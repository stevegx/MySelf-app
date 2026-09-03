// Integration tests share one PostgreSQL database and reset it between tests
// (see Infrastructure/DatabaseFixture.cs). Running them serially keeps one test's reset from
// pulling data out from under another.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
