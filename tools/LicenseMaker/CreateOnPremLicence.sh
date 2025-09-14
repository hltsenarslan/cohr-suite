# 1) OnPrem lisans
export LICENSE_MASTER_KEY='Wha7vA4xgiSfX8yfdw0436WAgF1jxDKXbzzwRDjp47MMmLTUwEPutGCT6gub7Ky4mLLxMQdizuYiZfcayxERYhgn7k8FfeBHH9NPzwatweivCeFfASQud8QBu7nwUrtLGKET2cbBKX60PfqzP5enBqbTxtmtgZhD7XFCwa6MAxZ3M28yWKC2PFBA1HbWyanY0ammQgrT5Tn98kf5EYCpgYb2ZEH41j4q0C19H4dVgbJ33fWpAwQXzvSwjcmAFTU6'
FP=$(dotnet run --project tools/HardwareProbe)
dotnet run --project tools/LicenseMaker -- \
  --mode=onprem \
  --fingerprint=$FP \
  --feature=perf=100 --feature=comp=0 \
  --expires=2026-12-31 \
  --out=./license.lic
  
  
  LICENSE_MASTER_KEY="Wha7vA4xgiSfX8yfdw0436WAgF1jxDKXbzzwRDjp47MMmLTUwEPutGCT6gub7Ky4mLLxMQdizuYiZfcayxERYhgn7k8FfeBHH9NPzwatweivCeFfASQud8QBu7nwUrtLGKET2cbBKX60PfqzP5enBqbTxtmtgZhD7XFCwa6MAxZ3M28yWKC2PFBA1HbWyanY0ammQgrT5Tn98kf5EYCpgYb2ZEH41j4q0C19H4dVgbJ33fWpAwQXzvSwjcmAFTU6" dotnet run -- \
    --mode=onprem \
    --expires=2026-01-01 \
    --issuer=CoHR \
    --fingerprint=36f695865ab477cf12bbea54d5c229077fcda35066873f00a79eff91d66374ba \
    --feature=perf=100 \
    --feature=comp=0 \
    --out=../../src/Core.Api/license.lic