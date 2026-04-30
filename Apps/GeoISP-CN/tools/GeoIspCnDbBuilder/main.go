package main

import (
	"bufio"
	"fmt"
	"io"
	"log"
	"net"
	"net/http"
	"os"
	"path/filepath"
	"slices"
	"strings"
	"time"

	"github.com/maxmind/mmdbwriter"
	"github.com/maxmind/mmdbwriter/mmdbtype"
)

type providerSpec struct {
	Key          string
	DisplayName  string
	Aliases      []string
	SourceFiles  []string
}

var providerSpecs = []providerSpec{
	{
		Key:         "chinanet",
		DisplayName: "China Telecom",
		Aliases:     []string{"ct", "chinatelecom", "china-telecom"},
		SourceFiles: []string{"chinatelecom_apnic.txt", "chinatelecom_ipv6_apnic.txt"},
	},
	{
		Key:         "cncgroup",
		DisplayName: "China Unicom",
		Aliases:     []string{"cu", "unicom_cnc", "unicom-cnc", "chinaunicom", "china-unicom"},
		SourceFiles: []string{"unicom_cnc_apnic.txt", "unicom_cnc_ipv6_apnic.txt"},
	},
	{
		Key:         "cmcc",
		DisplayName: "China Mobile",
		Aliases:     []string{"cm", "china-mobile", "chinamobile"},
		SourceFiles: []string{"cmcc_apnic.txt", "cmcc_ipv6_apnic.txt"},
	},
	{
		Key:         "chinabtn",
		DisplayName: "China Broadnet",
		Aliases:     []string{"cbn", "guangdian", "chinabroadnet", "china-broadnet"},
		SourceFiles: []string{"chinabtn_apnic.txt", "chinabtn_ipv6_apnic.txt"},
	},
	{
		Key:         "cernet",
		DisplayName: "China Education and Research Network",
		Aliases:     []string{"edu", "cernet-ap", "china-education-and-research-network"},
		SourceFiles: []string{"cernet_apnic.txt", "cernet_ipv6_apnic.txt"},
	},
}

func main() {
	output := filepath.Clean(filepath.Join("..", "..", "GeoIP2-ISP-CN.mmdb"))
	if len(os.Args) > 1 {
		output = os.Args[1]
	}

	writer, err := mmdbwriter.New(mmdbwriter.Options{
		DatabaseType: "GeoIP2-ISP-CN",
		Description: map[string]string{
			"en": "CN ISP prefix database built from ispip.clang.cn APNIC lists",
		},
		RecordSize: 24,
	})
	if err != nil {
		log.Fatal(err)
	}

	client := &http.Client{Timeout: 60 * time.Second}
	inserted := 0
	ownerByPrefix := make(map[string]string)

	for _, spec := range providerSpecs {
		record := buildRecord(spec)

		for _, sourceFile := range spec.SourceFiles {
			url := "https://ispip.clang.cn/" + sourceFile
			resp, err := fetch(client, url)
			if err != nil {
				log.Fatalf("download %s: %v", sourceFile, err)
			}

			scanner := bufio.NewScanner(resp)
			for scanner.Scan() {
				line := strings.TrimSpace(scanner.Text())
				if line == "" || strings.HasPrefix(line, "#") {
					continue
				}

				_, network, err := net.ParseCIDR(line)
				if err != nil {
					log.Fatalf("parse %s from %s: %v", line, sourceFile, err)
				}

				prefix := network.String()
				if previous, ok := ownerByPrefix[prefix]; ok && previous != spec.Key {
					log.Fatalf("duplicate prefix %s owned by both %s and %s", prefix, previous, spec.Key)
				}

				if err := writer.Insert(network, record); err != nil {
					log.Fatalf("insert %s: %v", prefix, err)
				}

				ownerByPrefix[prefix] = spec.Key
				inserted++
			}

			if err := scanner.Err(); err != nil {
				log.Fatalf("scan %s: %v", sourceFile, err)
			}
		}
	}

	outFile, err := os.Create(output)
	if err != nil {
		log.Fatal(err)
	}
	defer outFile.Close()

	if _, err := writer.WriteTo(outFile); err != nil {
		log.Fatal(err)
	}

	fmt.Printf("wrote %s with %d prefixes\n", output, inserted)
}

func buildRecord(spec providerSpec) mmdbtype.Map {
	aliases := slices.Clone(spec.Aliases)
	aliases = append(aliases, spec.Key)

	aliasSlice := mmdbtype.Slice{}
	for _, alias := range aliases {
		aliasSlice = append(aliasSlice, mmdbtype.String(alias))
	}

	return mmdbtype.Map{
		"provider":      mmdbtype.String(spec.Key),
		"provider_name": mmdbtype.String(spec.DisplayName),
		"aliases":       aliasSlice,
		"source":        mmdbtype.String("ispip.clang.cn/apnic"),
	}
}

func fetch(client *http.Client, url string) (*strings.Reader, error) {
	request, err := http.NewRequest(http.MethodGet, url, nil)
	if err != nil {
		return nil, err
	}

	request.Header.Set("User-Agent", "GeoISP-CN-GeoIspCnDbBuilder/1.0")

	response, err := client.Do(request)
	if err != nil {
		return nil, err
	}
	defer response.Body.Close()

	if response.StatusCode != http.StatusOK {
		return nil, fmt.Errorf("unexpected status %s", response.Status)
	}

	body, err := io.ReadAll(response.Body)
	if err != nil {
		return nil, err
	}

	return strings.NewReader(string(body)), nil
}
