# 1) OnPrem lisans
export LICENSE_MASTER_KEY='this-is-a-strong-32-byte-master-key!!!!'
FP=$(dotnet run --project tools/HardwareProbe)
dotnet run --project tools/LicenseMaker -- \
--mode=onprem \
--fingerprint=$FP \
--feature=perf=100 --feature=comp=0 \
--expires=2026-12-31 \
--out=license.lic

# 2) Cloud lisans (özellik/limit dosyada yok)
dotnet run --project tools/LicenseMaker -- \
--mode=cloud \
--expires=2026-12-31 \
--out=license.lic