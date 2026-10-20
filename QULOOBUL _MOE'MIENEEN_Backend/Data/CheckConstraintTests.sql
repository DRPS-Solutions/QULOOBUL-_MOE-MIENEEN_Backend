-- Proves the CHECK constraints from the AddRangeCheckConstraints migration reject bad data.
-- Each statement tries an invalid UPDATE; the whole run is rolled back, so nothing is changed.
-- Run:  sqlcmd -S .\SQLEXPRESS -d QuloobulMoeMieneen -E -C -W -i Data/CheckConstraintTests.sql
-- Expect: every row "rejected" except the "(control)" row, which is "accepted".
SET NOCOUNT ON;
SELECT (SELECT COUNT(*) FROM [Task]) AS Tasks, (SELECT COUNT(*) FROM [Report]) AS Reports, (SELECT COUNT(*) FROM [User]) AS Users;
BEGIN TRAN;
DECLARE @t TABLE (test varchar(60), result varchar(200));
BEGIN TRY UPDATE TOP (1) [Task] SET PercentComplete = 101; INSERT @t VALUES ('Task PercentComplete=101','NOT REJECTED'); END TRY BEGIN CATCH INSERT @t VALUES ('Task PercentComplete=101','rejected: '+ERROR_MESSAGE()); END CATCH
BEGIN TRY UPDATE TOP (1) [Task] SET PercentComplete = -1; INSERT @t VALUES ('Task PercentComplete=-1','NOT REJECTED'); END TRY BEGIN CATCH INSERT @t VALUES ('Task PercentComplete=-1','rejected: '+ERROR_MESSAGE()); END CATCH
BEGIN TRY UPDATE TOP (1) [Task] SET PercentComplete = 50; INSERT @t VALUES ('Task PercentComplete=50 (control)','accepted'); END TRY BEGIN CATCH INSERT @t VALUES ('Task PercentComplete=50 (control)','REJECTED: '+ERROR_MESSAGE()); END CATCH
BEGIN TRY UPDATE TOP (1) [Report] SET PeriodEnd = DATEADD(day,-1,PeriodStart); INSERT @t VALUES ('Report PeriodEnd<PeriodStart','NOT REJECTED'); END TRY BEGIN CATCH INSERT @t VALUES ('Report PeriodEnd<PeriodStart','rejected: '+ERROR_MESSAGE()); END CATCH
BEGIN TRY UPDATE TOP (1) [User] SET FailedLoginCount = -1; INSERT @t VALUES ('User FailedLoginCount=-1','NOT REJECTED'); END TRY BEGIN CATCH INSERT @t VALUES ('User FailedLoginCount=-1','rejected: '+ERROR_MESSAGE()); END CATCH
ROLLBACK;
SELECT test, LEFT(result,170) AS result FROM @t;
SELECT COUNT(*) AS Task_rows_with_bad_value_after_rollback FROM [Task] WHERE PercentComplete NOT BETWEEN 0 AND 100;
