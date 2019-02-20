# PeerCC - Callstats integration

## Requirements

* OpenSSL https://slproweb.com/products/Win32OpenSSL.html 
* Tested with version 1.1.1a 
> NOTE: The WebRTC SDK development environment requires the installation of Strawberry Perl and adding it to the system path.  It may be necessary to add the OpenSSL 1.1.1a installation location to the path to prevent issues.  To check the location of the openssl binary, type 'where openssl.exe' in the command prompt.   
    
## Callstats REST API authentication steps:

* Set `App ID`: copy from your application settings to Config.cs 
`localSettings.Values["appID"]`

* Set `secret string` (any string the user chooses) in Config.cs 
`localSettings.Values["secret"]`
    
* Open Command Prompt: 
`set RANDFILE=.rnd` 

* Generate private key: 
`openssl ecparam -genkey -name secp256k1 -noout -out privatekey.pem`

* Generate public key: 
`openssl ec -in privatekey.pem -pubout -out pubkey.pem`

* Copy public key to your application settings.
> Callstats dashboard (left side menu):       
> App Settings -> Security -> + New credential -> Key type: ECDSA public key   
   
* Set `key id`: copy from your application settings to Config.cs 
`localSettings.Values["keyID"]`  
> Callstats dashboard (left side menu):       
> App Settings -> Security -> Key Details -> View     
    
* Create certificate: 
`openssl req -new -x509 -days 1826 -key privatekey.pem -out certificate.crt`

* Create .p12 certificate, use `secret string` for password: 
`openssl pkcs12 -export -out ecc-key.p12 -inkey privatekey.pem -in certificate.crt`

* Add .p12 certificate to UWP project: 
> Open .appxmanifest Declarations tab     
> Select Certificates and Add      
> Add New: Store name: Root, Content: path to .p12 certificate      
> When `ecc-key.p12` is in the project remove it from Declarations tab   

