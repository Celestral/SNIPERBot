# SNIPERBot

## Things to do for setup: 
Make sure you have some way of managing *User Secrets* otherwise create an *appsettings.Development.json* file and put secrets in there. 

Secrets: 
``` json
{
  "Authentication": {
    "SniperToken": "<discordToken>",
    "BlobStorageSas": "<SASUrl_for_ProjectLocation>"
  },
  "Storage": {
    "ProjectLocation": "<BlobStorage_Projects.jsonLocation>"
  },
  "KeyVaultName": "<Keyvault_Name>"
}
```
