using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SafeDeal.Application.Common.Behaviors;
using SafeDeal.Application.Common.Caching;
using SafeDeal.Application.Common.Options;

namespace SafeDeal.Tests.Unit.Application;

/// <summary>
/// Le cache ne doit rien changer a ce que l'API repond : il doit seulement
/// changer le nombre de fois ou la base est interrogee. Ces tests verrouillent
/// cette frontiere.
/// </summary>
public class CachingBehaviorTests
{
    private sealed record Payload(string Value);

    private sealed record CachedQuery(int UserId) : IRequest<Payload>, ICachedQuery
    {
        public string CacheScope => CacheScopes.User(UserId);
        public string CacheKey => CacheKeys.VendorDashboard;
        public CacheProfile Profile => CacheProfile.Dashboard;
    }

    private sealed record PlainQuery : IRequest<Payload>;

    private readonly Mock<ICacheService> _cache = new();
    private readonly CacheOptions _options = new();

    private CachingBehavior<TRequest, Payload> Behavior<TRequest>() where TRequest : notnull =>
        new(_cache.Object,
            Options.Create(_options),
            NullLogger<CachingBehavior<TRequest, Payload>>.Instance);

    private static (RequestHandlerDelegate<Payload> Next, Func<int> Calls) Handler(Payload response)
    {
        var calls = 0;
        return (_ => { calls++; return Task.FromResult(response); }, () => calls);
    }

    [Fact]
    public async Task Une_requete_non_cachable_passe_droit_au_handler()
    {
        // Seules les requetes qui declarent ICachedQuery sont concernees : tout
        // le reste — commandes, lectures transactionnelles — ignore le cache.
        var (next, calls) = Handler(new Payload("frais"));

        var result = await Behavior<PlainQuery>().Handle(new PlainQuery(), next, default);

        result.Value.Should().Be("frais");
        calls().Should().Be(1);
        _cache.Verify(c => c.GetAsync<Payload>(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Une_entree_presente_evite_l_appel_au_handler()
    {
        _cache.Setup(c => c.GetAsync<Payload>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Payload("depuis-le-cache"));
        var (next, calls) = Handler(new Payload("depuis-la-base"));

        var result = await Behavior<CachedQuery>().Handle(new CachedQuery(7), next, default);

        result.Value.Should().Be("depuis-le-cache");
        calls().Should().Be(0);
    }

    [Fact]
    public async Task Une_entree_absente_declenche_le_calcul_puis_l_ecriture()
    {
        _cache.Setup(c => c.GetAsync<Payload>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payload?)null);
        var (next, calls) = Handler(new Payload("depuis-la-base"));

        var result = await Behavior<CachedQuery>().Handle(new CachedQuery(7), next, default);

        result.Value.Should().Be("depuis-la-base");
        calls().Should().Be(1);
        _cache.Verify(c => c.SetAsync(
            It.IsAny<string>(),
            It.Is<Payload>(p => p.Value == "depuis-la-base"),
            TimeSpan.FromSeconds(_options.DashboardSeconds),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task La_cle_porte_la_portee_et_la_generation()
    {
        // C'est ce qui rend l'invalidation instantanee : incrementer la
        // generation rend inatteignables toutes les cles de la portee.
        _cache.Setup(c => c.GenerationAsync(CacheScopes.User(7), It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);
        string? observed = null;
        _cache.Setup(c => c.GetAsync<Payload>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string key, CancellationToken _) => observed = key)
            .ReturnsAsync((Payload?)null);
        var (next, _) = Handler(new Payload("x"));

        await Behavior<CachedQuery>().Handle(new CachedQuery(7), next, default);

        observed.Should().Be("cache:dash:u:7:v4:vendor");
    }

    [Fact]
    public async Task Une_generation_incrementee_change_la_cle_lue()
    {
        var keys = new List<string>();
        _cache.Setup(c => c.GetAsync<Payload>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string key, CancellationToken _) => keys.Add(key))
            .ReturnsAsync((Payload?)null);
        var (next, _) = Handler(new Payload("x"));

        _cache.Setup(c => c.GenerationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        await Behavior<CachedQuery>().Handle(new CachedQuery(7), next, default);

        _cache.Setup(c => c.GenerationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(2);
        await Behavior<CachedQuery>().Handle(new CachedQuery(7), next, default);

        keys.Should().HaveCount(2);
        keys[0].Should().NotBe(keys[1]);
    }

    [Fact]
    public async Task Deux_utilisateurs_ne_partagent_jamais_une_entree()
    {
        var keys = new List<string>();
        _cache.Setup(c => c.GetAsync<Payload>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string key, CancellationToken _) => keys.Add(key))
            .ReturnsAsync((Payload?)null);
        var (next, _) = Handler(new Payload("x"));

        await Behavior<CachedQuery>().Handle(new CachedQuery(7), next, default);
        await Behavior<CachedQuery>().Handle(new CachedQuery(8), next, default);

        keys[0].Should().NotBe(keys[1]);
    }

    [Fact]
    public async Task Le_cache_desactive_par_configuration_est_transparent()
    {
        _options.Enabled = false;
        var (next, calls) = Handler(new Payload("frais"));

        var result = await Behavior<CachedQuery>().Handle(new CachedQuery(7), next, default);

        result.Value.Should().Be("frais");
        calls().Should().Be(1);
        _cache.Verify(c => c.GetAsync<Payload>(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _cache.Verify(c => c.SetAsync(
            It.IsAny<string>(), It.IsAny<Payload>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Une_reponse_nulle_n_est_pas_figee_dans_le_cache()
    {
        _cache.Setup(c => c.GetAsync<Payload>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payload?)null);
        RequestHandlerDelegate<Payload> next = _ => Task.FromResult<Payload>(null!);

        await Behavior<CachedQuery>().Handle(new CachedQuery(7), next, default);

        _cache.Verify(c => c.SetAsync(
            It.IsAny<string>(), It.IsAny<Payload>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Les_compteurs_d_administration_expirent_plus_vite_que_les_tableaux_de_bord()
    {
        // Ils agregent toute la plateforme et ne sont pas tous couverts par une
        // invalidation explicite : leur peremption doit etre plus courte.
        _options.DashboardSeconds.Should().BeGreaterThan(_options.AdminStatsSeconds);
        await Task.CompletedTask;
    }
}
