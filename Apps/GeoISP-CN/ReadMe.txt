GeoISP-CN
=========

This app creates APP records that return A/AAAA records or CNAME records based on the client country and ISP/ASN. It supports EDNS Client Subnet (ECS).

Database lookup order:

1. GeoIP2-ISP-CN.mmdb
2. GeoIP2-ISP.mmdb or GeoLite2-ASN.mmdb
3. GeoIP2-Country.mmdb or GeoLite2-Country.mmdb

GeoIP2-ISP-CN.mmdb is a CN carrier prefix database intended for internal routing. It contains common provider keys for:

- China Telecom: ct, chinatelecom
- China Unicom: cu, chinaunicom
- China Mobile: cm, chinamobile, cmi
- China Broadnet: cbn, chinabroadnet, chinabroadcast
- CERNET: cernet, chinaeducation

APP record data supports mixed branches. A branch value can be either an array of IP addresses or a domain name string.

Example:

{
  "CN": {
    "providers": {
      "ct": "154.39.66.250",
      "chinatelecom": "154.39.66.250",
      "default": [
        "82.26.72.199",
        "82.26.72.200"
      ]
    }
  },
  "default": [
    "82.26.72.199",
    "82.26.72.200"
  ]
}

Provider keys are normalized to lowercase, spaces become '-', and punctuation is removed. You can use '{CountryCode}' and '{ProviderKey}' variables in CNAME branches.

To update MaxMind databases, zip the .mmdb file and use the manual Update option. Supported file names include GeoIP2-Country.mmdb, GeoLite2-Country.mmdb, GeoIP2-ISP.mmdb, GeoLite2-ASN.mmdb, and GeoIP2-ISP-CN.mmdb.
