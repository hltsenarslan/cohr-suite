export LICENSE_MASTER_KEY='Wha7vA4xgiSfX8yfdw0436WAgF1jxDKXbzzwRDjp47MMmLTUwEPutGCT6gub7Ky4mLLxMQdizuYiZfcayxERYhgn7k8FfeBHH9NPzwatweivCeFfASQud8QBu7nwUrtLGKET2cbBKX60PfqzP5enBqbTxtmtgZhD7XFCwa6MAxZ3M28yWKC2PFBA1HbWyanY0ammQgrT5Tn98kf5EYCpgYb2ZEH41j4q0C19H4dVgbJ33fWpAwQXzvSwjcmAFTU6'
FP=$(dotnet run --project tools/HardwareProbe)

dotnet run --project tools/LicenseMaker -- \
  --mode=cloud \
  --expires=2026-12-31 \
  --out=./license.lic