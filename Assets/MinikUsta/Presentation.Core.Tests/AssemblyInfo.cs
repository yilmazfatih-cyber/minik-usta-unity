#nullable enable
// Every presentation test runs under the invariant culture, like the core tests (REVIEW_PHASE2 R-06): number texts in
// assertion messages and parsed token values never depend on the developer's regional settings.
[assembly: NUnit.Framework.SetCulture("")]
