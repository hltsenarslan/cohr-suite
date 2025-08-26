namespace Core.Api.Domain
{
    // Küçük harf isimler: DB'deki 'slug' / 'host' stringleri ile birebir uyumlu
    public enum PathMode
    {
        host = 0,
        slug = 1
    }
}