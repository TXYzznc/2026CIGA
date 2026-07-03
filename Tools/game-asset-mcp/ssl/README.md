# SSL Certificates for HTTPS Support

This directory is where SSL certificates should be placed for enabling HTTPS support when running the Game Asset Generator with the SSE transport.

## Required Files

- `key.pem`: The private key file
- `cert.pem`: The certificate file

## Generating Self-Signed Certificates

For development and testing purposes, you can generate self-signed certificates using OpenSSL:

### On Linux/macOS:

```bash
openssl req -x509 -newkey rsa:4096 -keyout key.pem -out cert.pem -days 365 -nodes
```

### On Windows (with OpenSSL installed):

```powershell
openssl req -x509 -newkey rsa:4096 -keyout key.pem -out cert.pem -days 365 -nodes
```

When prompted, you can provide your information or accept the defaults.

## Using the Certificates

Once you have the certificate files in this directory, you can run the server with HTTPS support:

```bash
node index.js --sse --https
```

## Security Note

Self-signed certificates are suitable for development and testing but should not be used in production environments. For production use, obtain certificates from a trusted Certificate Authority (CA).