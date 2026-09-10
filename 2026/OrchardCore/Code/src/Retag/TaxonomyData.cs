namespace Retag;

/// <summary>
/// 24 top-level municipal categories x 12 children + the 24 parents = 312 terms.
/// Fictional councils, real-shaped subject matter.
/// </summary>
public static class TaxonomyData
{
    public static readonly (string Id, string[] Children)[] Categories =
    [
        ("community-events", ["festivals", "farmers-markets", "street-fairs", "public-workshops",
            "volunteer-days", "seasonal-celebrations", "neighbourhood-meetings", "sports-tournaments",
            "community-fundraisers", "holiday-programmes", "open-house-days", "civic-ceremonies"]),

        ("public-safety", ["emergency-services", "fire-prevention", "flood-warnings", "storm-readiness",
            "police-liaison", "neighbourhood-watch", "road-safety", "water-safety",
            "hazard-reporting", "evacuation-routes", "public-alerts", "first-aid-training"]),

        ("waste-services", ["kerbside-collection", "recycling-centres", "green-waste", "bulky-item-pickup",
            "hazardous-waste", "composting", "bin-requests", "collection-calendars",
            "litter-reporting", "illegal-dumping", "textile-recycling", "electronics-disposal"]),

        ("council-notices", ["meeting-agendas", "meeting-minutes", "public-consultations", "statutory-notices",
            "tender-notices", "bylaw-changes", "appointment-notices", "budget-notices",
            "closure-notices", "hearing-notices", "policy-drafts", "annual-reports"]),

        ("planning-and-development", ["planning-applications", "zoning-maps", "development-plans",
            "subdivision-consents", "heritage-listings", "design-guidelines", "land-use-appeals",
            "growth-strategy", "infrastructure-plans", "resource-consents", "planning-fees", "structure-plans"]),

        ("parks-and-recreation", ["playgrounds", "sports-fields", "walking-trails", "public-pools",
            "community-gardens", "dog-parks", "picnic-areas", "reserve-bookings",
            "skate-parks", "boat-ramps", "park-maintenance", "outdoor-fitness"]),

        ("roads-and-transport", ["road-works", "street-lighting", "footpaths", "cycleways",
            "parking-permits", "bus-services", "traffic-calming", "bridge-maintenance",
            "snow-clearing", "street-signage", "road-closures", "park-and-ride"]),

        ("libraries", ["branch-hours", "borrowing-rules", "digital-collections", "story-time",
            "study-rooms", "interlibrary-loans", "local-history", "author-talks",
            "computer-access", "reading-programmes", "library-events", "library-membership"]),

        ("housing", ["social-housing", "tenancy-support", "housing-registers", "rent-assistance",
            "homelessness-services", "housing-standards", "accessible-housing", "emergency-accommodation",
            "landlord-guidance", "housing-repairs", "allocations-policy", "housing-appeals"]),

        ("business-and-licensing", ["business-permits", "food-licences", "liquor-licences", "market-stalls",
            "signage-permits", "event-permits", "trade-waste", "home-businesses",
            "licence-renewals", "inspection-reports", "mobile-vendors", "entertainment-licences"]),

        ("environment", ["air-quality", "noise-control", "tree-protection", "biodiversity",
            "coastal-management", "contaminated-land", "climate-action", "energy-efficiency",
            "pest-control", "waterway-health", "conservation-grants", "environmental-monitoring"]),

        ("water-and-drainage", ["water-supply", "water-quality", "stormwater", "wastewater",
            "drainage-maintenance", "water-restrictions", "leak-reporting", "backflow-prevention",
            "septic-systems", "flood-mitigation", "water-billing", "hydrant-access"]),

        ("health-services", ["immunisation", "public-health-alerts", "food-safety", "sexual-health",
            "mental-health", "smoking-cessation", "health-inspections", "communicable-disease",
            "health-promotion", "clinic-locations", "screening-programmes", "environmental-health"]),

        ("social-care", ["aged-care", "disability-support", "child-protection", "family-services",
            "respite-care", "carer-support", "meals-on-wheels", "day-centres",
            "advocacy-services", "safeguarding", "home-help", "social-work-referrals"]),

        ("education", ["school-zones", "early-childhood", "adult-learning", "school-transport",
            "school-holiday-clubs", "scholarships", "truancy-services", "special-education",
            "school-meals", "parent-resources", "careers-guidance", "tutoring-support"]),

        ("elections", ["voter-registration", "polling-places", "candidate-information", "postal-voting",
            "election-results", "electoral-boundaries", "referendums", "election-notices",
            "campaign-rules", "scrutineers", "by-elections", "voting-accessibility"]),

        ("finance-and-rates", ["rates-notices", "rates-rebates", "payment-plans", "direct-debit",
            "valuation-objections", "arrears-support", "fees-and-charges", "financial-statements",
            "grants-funding", "invoicing", "penalty-remission", "rates-calculator"]),

        ("animal-services", ["dog-registration", "animal-control", "lost-and-found-pets", "impound-services",
            "wildlife-reporting", "livestock-permits", "barking-complaints", "dangerous-dogs",
            "microchipping", "animal-welfare", "beekeeping", "pet-adoption"]),

        ("cemeteries", ["burial-plots", "cremation-services", "memorial-gardens", "grave-maintenance",
            "plot-purchase", "burial-records", "ash-interment", "headstone-permits",
            "cemetery-hours", "funeral-directors", "war-graves", "natural-burial"]),

        ("tourism", ["visitor-centres", "walking-tours", "accommodation-guide", "local-attractions",
            "event-calendar", "travel-advice", "camping-grounds", "scenic-routes",
            "visitor-parking", "regional-maps", "tour-operators", "seasonal-guides"]),

        ("arts-and-culture", ["public-art", "museums", "galleries", "heritage-trails",
            "performance-venues", "cultural-grants", "artist-residencies", "community-theatre",
            "music-programmes", "craft-markets", "cultural-festivals", "archives-access"]),

        ("building-control", ["building-consents", "code-compliance", "inspections-booking", "structural-reviews",
            "earthquake-strengthening", "pool-fencing", "accessibility-compliance", "demolition-permits",
            "building-warrants", "site-safety", "occupancy-certificates", "consent-exemptions"]),

        ("procurement", ["tender-process", "supplier-registration", "contract-awards", "procurement-policy",
            "panel-agreements", "request-for-proposal", "evaluation-criteria", "contract-variations",
            "social-procurement", "supplier-payments", "conflict-declarations", "tender-debriefs"]),

        ("records-and-foi", ["information-requests", "records-retention", "privacy-requests", "public-registers",
            "archive-access", "data-releases", "request-fees", "response-timeframes",
            "redaction-policy", "appeals-process", "open-data", "document-search"]),
    ];

    /// <summary>
    /// The only difference between v41 and v42. Same 312 ids, same labels, same
    /// shape - two guidance strings change. Nothing throws, which is the point.
    /// </summary>
    public const string CommunityEventsV41 =
        "Gatherings, markets and civic occasions. Also file preparedness sessions, " +
        "readiness workshops and emergency drop-ins here when they are run as public events.";

    public const string CommunityEventsV42 =
        "Gatherings, markets and civic occasions. Preparedness and readiness content " +
        "belongs under public-safety, even when delivered as a public event.";

    public const string PublicSafetyV41 =
        "Policing, hazards and alerts. Preparedness education delivered as community " +
        "programming is filed under community-events.";

    public const string PublicSafetyV42 =
        "Policing, hazards and alerts. All emergency preparedness content belongs here, " +
        "including readiness workshops, drop-ins and household planning guides.";
}
