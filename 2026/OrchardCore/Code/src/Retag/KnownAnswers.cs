namespace Retag;

/// <summary>
/// The 20 known-answer cases the canary runs. Rung 1 of the ladder: a small set
/// of items where the right answer is already agreed, checked before anything
/// commits.
///
/// The margin is how clear-cut the case is. Release 18 adds "prefer broader
/// terms", which only bites where the margin is thin - so OBVIOUS and
/// HUMAN-VERIFIED survive it and ARGUED does not. The two ARGUED cases that
/// still pass are the two whose correct answer is already top-level: there is
/// nothing broader to reach for.
/// </summary>
public static class KnownAnswers
{
    public const int Total = 20;
    public const int ObviousCount = 6;
    public const int HumanVerifiedCount = 6;
    public const int ArguedCount = 8;

    public const int PassingUnderRelease17 = 20;
    public const int PassingUnderRelease18 = 14;

    /// <summary>Release 18 broadens to the parent term when the margin is below this.</summary>
    public const double BroadenThreshold = 0.30;

    public const string Release18Instruction =
        "Prefer broader terms. Editors find deep hierarchies confusing.";

    public static readonly string[] Categories = ["OBVIOUS", "HUMAN-VERIFIED", "ARGUED"];

    public static List<KnownAnswer> Build() =>
    [
        // OBVIOUS - the answer is in the title.
        Case("ka-01", "OBVIOUS", "kerbside-collection", "waste-services",
            "Kerbside collection moves to Wednesday in Ashwood",
            "Bin day changes for the Ashwood collection round from the first of next month.", 0.88),
        Case("ka-02", "OBVIOUS", "dog-registration", "animal-services",
            "Dog registration renewals open on 1 July",
            "Renew your dog registration online or at any service centre before the due date.", 0.91),
        Case("ka-03", "OBVIOUS", "polling-places", "elections",
            "Polling places for the district election",
            "Locations, opening hours and accessibility details for every polling place.", 0.86),
        Case("ka-04", "OBVIOUS", "burial-plots", "cemeteries",
            "Purchasing a burial plot at Riverside Cemetery",
            "Plot availability, pricing and the paperwork required to reserve a burial plot.", 0.84),
        Case("ka-05", "OBVIOUS", "building-consents", "building-control",
            "Applying for a building consent",
            "What to submit, current processing timeframes and the fees payable on lodgement.", 0.89),
        Case("ka-06", "OBVIOUS", "rates-notices", "finance-and-rates",
            "Your quarterly rates notice explained",
            "A line-by-line walkthrough of the charges that appear on a quarterly rates notice.", 0.87),

        // HUMAN-VERIFIED - an editor confirmed these by hand.
        Case("ka-07", "HUMAN-VERIFIED", "green-waste", "waste-services",
            "Garden waste bins over the summer period",
            "Extra collections run through summer. Only untreated garden material is accepted.", 0.62),
        Case("ka-08", "HUMAN-VERIFIED", "tenancy-support", "housing",
            "Help if you are struggling to pay rent",
            "Advice, mediation and hardship grants for tenants at risk of falling behind.", 0.58),
        Case("ka-09", "HUMAN-VERIFIED", "school-transport", "education",
            "School bus routes for the new term",
            "Route maps, stop times and how to apply for a subsidised transport pass.", 0.66),
        Case("ka-10", "HUMAN-VERIFIED", "noise-control", "environment",
            "Reporting persistent noise from a neighbouring property",
            "How the council investigates noise complaints and what evidence helps a case.", 0.61),
        Case("ka-11", "HUMAN-VERIFIED", "walking-trails", "parks-and-recreation",
            "Ridge Loop trail reopens after slip repairs",
            "The upper section of the Ridge Loop is open again following remedial work.", 0.57),
        Case("ka-12", "HUMAN-VERIFIED", "food-licences", "business-and-licensing",
            "Food licence renewals for mobile traders",
            "Renewal windows, inspection requirements and the fee schedule for mobile traders.", 0.64),

        // ARGUED - reasonable people disagree. This is where a broadening
        // instruction does its damage, and every failure lands on the parent.
        Case("ka-13", "ARGUED", "storm-readiness", "public-safety",
            "What to do before a severe weather warning",
            "Preparing a property and a household ahead of a forecast severe weather event.", 0.22),
        Case("ka-14", "ARGUED", "flood-mitigation", "water-and-drainage",
            "Sandbag collection points ahead of high tides",
            "Where to collect sandbags and how to place them around a property.", 0.19),
        Case("ka-15", "ARGUED", "community-gardens", "parks-and-recreation",
            "Allotment waiting lists at Corner Green",
            "How allotment plots are allocated and how long the current waiting list is.", 0.24),
        Case("ka-16", "ARGUED", "emergency-accommodation", "housing",
            "Where to go tonight if you have nowhere to stay",
            "After-hours contacts and locations for emergency accommodation placements.", 0.17),
        Case("ka-17", "ARGUED", "public-health-alerts", "health-services",
            "Boil water notice for the Eastfield supply zone",
            "Residents in the Eastfield zone should boil drinking water until further notice.", 0.21),
        Case("ka-18", "ARGUED", "heritage-listings", "planning-and-development",
            "Proposed heritage listing for the Old Granary",
            "The council is consulting on adding the Old Granary to the heritage schedule.", 0.26),

        // Top-level answers: broadening has nowhere to go, so these two survive.
        Case("ka-19", "ARGUED", "public-safety", null,
            "Winter safety across the district",
            "A round-up of winter hazards, gritting routes and who to call out of hours.", 0.23),
        Case("ka-20", "ARGUED", "community-events", null,
            "What is on across the district this month",
            "A monthly round-up of markets, fairs, civic occasions and neighbourhood meetings.", 0.28),
    ];

    private static KnownAnswer Case(
        string id, string category, string expected, string? parent,
        string title, string body, double margin) =>
        new(id, category, $"known-answers/{id}", title, body, expected, parent, margin);

    /// <summary>
    /// The seeded stand-in for the agent under a given release. Release 17 answers
    /// the case. Release 18 reaches for the parent whenever the case is thin.
    /// </summary>
    public static string Answer(KnownAnswer c, int release) =>
        release >= 18 && c.Margin < BroadenThreshold
            ? c.ParentTerm ?? c.ExpectedTerm
            : c.ExpectedTerm;

    public static bool Passes(KnownAnswer c, int release) => Answer(c, release) == c.ExpectedTerm;
}
