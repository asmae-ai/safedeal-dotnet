using System.IO.Compression;
using Microsoft.AspNetCore.ResponseCompression;

namespace SafeDeal.API.Extensions;

/// <summary>
/// Compression des réponses.
///
/// Rien ne la fournissait jusqu'ici : ni ASP.NET Core, qui ne l'active pas par
/// défaut, ni l'infrastructure Docker de ce dépôt, qui expose l'API
/// directement. Les charges utiles les plus lourdes de l'API — un tableau de
/// bord avec ses douze mois de série et son fil d'activité, une liste
/// d'administration — sont du JSON très répétitif, exactement ce que gzip
/// réduit le mieux.
/// </summary>
public static class CompressionExtensions
{
    public static IServiceCollection AddSafeDealCompression(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddResponseCompression(options =>
        {
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();

            // Le défaut couvre déjà application/json, text/* et application/xml.
            // On y ajoute les types que l'API produit et qui n'y figurent pas.
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
            [
                "application/problem+json",
                "image/svg+xml"
            ]);

            // Jamais les binaires deja compresses : les recompresser coute du
            // temps processeur pour, au mieux, quelques octets.
            options.ExcludedMimeTypes = ["image/png", "image/jpeg", "image/webp", "application/pdf"];

            // Desactive par defaut, et c'est deliberé. Compresser une reponse
            // chiffree qui melange un secret (jeton) et une valeur controlee par
            // l'appelant ouvre la voie a BREACH : la taille compressee trahit
            // alors le secret. En production l'API est derriere un proxy qui
            // termine TLS, la requete arrive en clair, et la compression
            // s'applique donc normalement. Le drapeau reste la pour les
            // deploiements qui exposent directement l'API et acceptent ce risque.
            options.EnableForHttps = configuration.GetValue<bool?>("Compression:EnableForHttps") ?? false;
        });

        // Fastest, pas Optimal : Brotli au niveau maximal passe des dizaines de
        // millisecondes sur une reponse de quelques dizaines de kilo-octets, pour
        // un gain de taille marginal. Sur une API, la latence coute plus cher que
        // ces quelques octets.
        services.Configure<BrotliCompressionProviderOptions>(
            options => options.Level = CompressionLevel.Fastest);
        services.Configure<GzipCompressionProviderOptions>(
            options => options.Level = CompressionLevel.Fastest);

        return services;
    }
}
