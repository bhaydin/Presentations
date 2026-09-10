namespace Retag;

public sealed record Tenant(int Index, string Id, string Name, int ItemCount);

public sealed record Term(string Id, string? Parent, string Label, string Guidance);

public sealed record Taxonomy(int Version, string Name, List<Term> Terms)
{
    private Dictionary<string, Term>? _byId;
    public Term this[string id] => (_byId ??= Terms.ToDictionary(t => t.Id))[id];
    public bool Has(string id) => (_byId ??= Terms.ToDictionary(t => t.Id)).ContainsKey(id);
}

/// <summary>
/// A content item. <see cref="TrueTerm"/> is the hidden ground truth: it exists
/// so the test suite can assert the corpus, and nothing in the demo path reads it.
/// </summary>
public sealed record ContentItem(
    string Id,
    string Tenant,
    string Title,
    string Body,
    string TrueTerm,
    bool EmergencyPreparedness);

/// <summary>What the agent decided on the night of the run.</summary>
public sealed record Decision(
    string ItemId,
    string Tenant,
    List<string> Chosen,
    int TaxonomyVersion,
    string Timestamp,
    List<string> Considered,
    string Rationale);

public sealed record KnownAnswer(
    string Id,
    string Category,
    string ItemId,
    string Title,
    string Body,
    string ExpectedTerm,
    string? ParentTerm,
    double Margin);
