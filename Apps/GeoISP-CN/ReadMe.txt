GeoISP-CN
=========

This app creates APP records that return A, AAAA, CNAME, or ANAME records based on the client country and ISP/ASN. It supports EDNS Client Subnet (ECS).

Database lookup order:

1. GeoIP2-Country.mmdb or GeoLite2-Country.mmdb for country code
2. GeoIP2-ISP-CN.xdb
3. GeoIP2-ISP-CN.mmdb
4. GeoIP2-ISP.mmdb or GeoLite2-ASN.mmdb

GeoIP2-ISP-CN.xdb is an ip2region xdb database. It reads country code, ISP, and ASN fields. CN ISP names are mapped to common provider keys. If GeoIP2-ISP-CN.xdb is missing and the app folder contains exactly one .xdb file, that file is used.

GeoIP2-ISP-CN.mmdb is a CN carrier prefix database intended for internal routing. It contains common provider keys for:

- China Telecom: ct, chinatelecom
- China Unicom: cu, chinaunicom
- China Mobile: cm, chinamobile, cmi
- China Broadnet: cbn, chinabroadnet, chinabroadcast
- CERNET: cernet, chinaeducation

APP record data supports mixed branches. A branch value should be a JSON record object. If CNAME or ANAME and the requested A/AAAA type both exist in the same branch, the app randomly returns one response type at 1:1. CNAME/ANAME and A/AAAA are never returned together. CNAME and ANAME values can be a single absolute domain name or an array of absolute domain names selected randomly at equal weight.

Example:

{
  "CN": {
    "ct": {
      "A": [
        "154.39.66.250"
      ],
      "CNAME": [
        "ct1.example.com.",
        "ct2.example.com."
      ]
    },
    "chinatelecom": {
      "A": [
        "154.39.66.250"
      ]
    },
    "default": {
      "A": [
        "82.26.72.199",
        "82.26.72.200"
      ],
      "AAAA": [
        "2001:db8::1"
      ]
    }
  },
  "default": {
    "CNAME": [
      "global1.example.com.",
      "global2.example.com."
    ]
  }
}

Provider keys are normalized to lowercase, spaces become '-', and punctuation is removed. You can use '{CountryCode}' and '{ProviderKey}' variables in CNAME and ANAME branches.

To update databases, zip the database file and use the manual Update option. Supported file names include GeoIP2-Country.mmdb, GeoLite2-Country.mmdb, GeoIP2-ISP.mmdb, GeoLite2-ASN.mmdb, GeoIP2-ISP-CN.mmdb, and GeoIP2-ISP-CN.xdb.
