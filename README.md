# PeerCC

## Requirements

* OpenSSL https://slproweb.com/products/Win32OpenSSL.html 

## CallStatsClient authentication steps:

* Rename PeerCC/Client/RenameToConfig.cs class and file to Config.cs

* Set `App ID`: copy from your application settings to Config.cs 
`localSettings.Values["appID"]`

* Set `secret string` in Config.cs 
`localSettings.Values["secret"]`

* Open Command Prompt: 
`set RANDFILE=.rnd` 

* Generate private key: 
`openssl ecparam -genkey -name secp256k1 -noout -out privatekey.pem`

* Generate public key: 
`openssl ec -in privatekey.pem -pubout -out pubkey.pem`

* Copy public key to your application settings.

* Set `key id`: copy from your application settings to Config.cs 
`localSettings.Values["keyID"]` 

* Create certificate: 
`openssl req -new -x509 -days 1826 -key privatekey.pem -out certificate.crt`

* Create .p12 certificate, use `secret string` for password: 
`openssl pkcs12 -export -out ecc-key.p12 -inkey privatekey.pem -in certificate.crt`

* Add .p12 certificate to UWP project: 
> Open .appxmanifest Declarations tab     
> Select Certificates and Add      
> Add New: Store name: Root, Content: path to .p12 certificate      
> When `ecc-key.p12` is in the project remove it from Declarations tab   

