using FluentAssertions;
using SafeDeal.Application.Common.Models;

namespace SafeDeal.Tests.Unit.Application;

/// <summary>
/// Les bornes de pagination sont partagees par toutes les listes de l'API :
/// une erreur ici se voit sur chaque ecran a la fois.
/// </summary>
public class PagingTests
{
    private sealed record OptionalQuery(int? Page, int PageSize) : IOptionallyPagedQuery;

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(7, 7)]
    public void Une_page_hors_bornes_est_ramenee_a_la_premiere(int requested, int expected)
    {
        // Ramener plutot que rejeter : un client qui demandait la page 0 recevait
        // une erreur serveur pour un cas parfaitement rattrapable.
        Paging.NormalizePage(requested).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, Paging.DefaultPageSize)]
    [InlineData(-1, Paging.DefaultPageSize)]
    [InlineData(15, 15)]
    [InlineData(100, 100)]
    [InlineData(5000, Paging.MaxPageSize)]
    public void Une_taille_de_page_hors_bornes_est_plafonnee(int requested, int expected)
    {
        // Le plafond empeche un client d'exiger la table entiere en une requete.
        Paging.NormalizePageSize(requested).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, 20, 1)]
    [InlineData(1, 20, 1)]
    [InlineData(20, 20, 1)]
    [InlineData(21, 20, 2)]
    [InlineData(40, 20, 2)]
    [InlineData(41, 20, 3)]
    public void La_derniere_page_compte_les_pages_reellement_servies(int total, int pageSize, int expected)
    {
        Paging.LastPage(total, pageSize).Should().Be(expected);
    }

    [Fact]
    public void Une_liste_vide_compte_une_page_et_non_zero()
    {
        // « page 1 sur 0 » n'a pas de sens a l'ecran, et un client qui borne sa
        // navigation sur last_page se retrouvait sans page valide.
        Paging.LastPage(total: 0, pageSize: 20).Should().Be(1);
    }

    [Fact]
    public void La_derniere_page_respecte_le_plafond_de_taille()
    {
        // Une taille demandee au-dela du plafond ne doit pas faire annoncer
        // moins de pages qu'il n'y en aura reellement.
        Paging.LastPage(total: 500, pageSize: 5000).Should().Be(5);
    }

    // ------------------------------------------------------ pagination optionnelle

    [Fact]
    public void Sans_page_demandee_la_liste_reste_entiere()
    {
        var query = new OptionalQuery(Page: null, PageSize: 2);
        var source = Enumerable.Range(1, 10).AsQueryable();

        source.Slice(query).Should().HaveCount(10);
        query.IsPaginated().Should().BeFalse();
    }

    [Fact]
    public void Une_liste_entiere_s_annonce_comme_une_page_unique()
    {
        var query = new OptionalQuery(Page: null, PageSize: 2);

        var result = Enumerable.Range(1, 10).ToList().ToResult(query, total: 10);

        result.CurrentPage.Should().Be(1);
        result.LastPage.Should().Be(1);
        result.Total.Should().Be(10);
    }

    [Fact]
    public void Une_page_demandee_decoupe_la_liste()
    {
        var query = new OptionalQuery(Page: 2, PageSize: 3);
        var source = Enumerable.Range(1, 10).AsQueryable();

        source.Slice(query).Should().Equal(4, 5, 6);
    }

    [Fact]
    public void Une_page_au_dela_du_total_rend_une_tranche_vide()
    {
        var query = new OptionalQuery(Page: 99, PageSize: 3);
        var source = Enumerable.Range(1, 10).AsQueryable();

        var page = source.Slice(query).ToList();

        page.Should().BeEmpty();
        page.ToResult(query, total: 10).LastPage.Should().Be(4, "le total ne depend pas de la page demandee");
    }
}
