-- Proves the CHECK constraints (AddRangeCheckConstraints + AddAllowedValueCheckConstraints) reject bad data.
-- Each test runs one invalid UPDATE against an existing row; the whole run is rolled back, so nothing changes.
-- Run:  sqlcmd -S .\SQLEXPRESS -d QuloobulMoeMieneen -E -C -W -i Data/CheckConstraintTests.sql
-- Expect: every "expect" = reject row shows the constraint that stopped it, and every accept (control) row
-- shows "accepted". Any "NOT REJECTED" / "REJECTED (unexpected)" line is a failure. Needs >= 1 row per table.
SET NOCOUNT ON;
DECLARE @tests TABLE (id int IDENTITY, expect varchar(6), test varchar(60), stmt nvarchar(400));
INSERT @tests (expect, test, stmt) VALUES
 ('reject', 'Task.PercentComplete = 101',        N'UPDATE TOP (1) [Task] SET PercentComplete = 101'),
 ('reject', 'Task.PercentComplete = -1',         N'UPDATE TOP (1) [Task] SET PercentComplete = -1'),
 ('accept', 'Task.PercentComplete = 50',         N'UPDATE TOP (1) [Task] SET PercentComplete = 50'),
 ('reject', 'Report.PeriodEnd < PeriodStart',    N'UPDATE TOP (1) [Report] SET PeriodEnd = DATEADD(day, -1, PeriodStart)'),
 ('reject', 'User.FailedLoginCount = -1',        N'UPDATE TOP (1) [User] SET FailedLoginCount = -1'),
 ('reject', 'Task.Status = ''Bogus''',           N'UPDATE TOP (1) [Task] SET Status = ''Bogus'''),
 ('accept', 'Task.Status = ''Blocked''',         N'UPDATE TOP (1) [Task] SET Status = ''Blocked'''),
 ('reject', 'Task.Priority = ''Urgent''',        N'UPDATE TOP (1) [Task] SET Priority = ''Urgent'''),
 ('accept', 'Task.Priority = ''Medium''',        N'UPDATE TOP (1) [Task] SET Priority = ''Medium'''),
 ('reject', 'Task_Status_History = ''Bogus''',   N'UPDATE TOP (1) [Task_Status_History] SET NewStatus = ''Bogus'''),
 ('reject', 'Resource_Request.Status = ''Bogus''',   N'UPDATE TOP (1) [Resource_Request] SET Status = ''Bogus'''),
 ('accept', 'Resource_Request.Status = ''Approved''',N'UPDATE TOP (1) [Resource_Request] SET Status = ''Approved'''),
 ('reject', 'Resource_Request.Priority = ''Urgent''',N'UPDATE TOP (1) [Resource_Request] SET Priority = ''Urgent'''),
 ('reject', 'Request_Status_History = ''Bogus''',N'UPDATE TOP (1) [Request_Status_History] SET NewStatus = ''Bogus'''),
 ('reject', 'User.Status = ''Bogus''',           N'UPDATE TOP (1) [User] SET Status = ''Bogus'''),
 ('accept', 'User.Status = ''Locked''',          N'UPDATE TOP (1) [User] SET Status = ''Locked'''),
 ('reject', 'Project.Status = ''Bogus''',        N'UPDATE TOP (1) [Project] SET Status = ''Bogus'''),
 ('accept', 'Project.Status = ''Closed''',       N'UPDATE TOP (1) [Project] SET Status = ''Closed'''),
 ('reject', 'Task_Schedule.DayOfWeek = ''Funday''',  N'UPDATE TOP (1) [Task_Schedule] SET DayOfWeek = ''Funday'''),
 ('accept', 'Task_Schedule.DayOfWeek = ''Friday''',  N'UPDATE TOP (1) [Task_Schedule] SET DayOfWeek = ''Friday''');

DECLARE @out TABLE (id int, test varchar(60), outcome varchar(120));
DECLARE @id int = 1, @max int = (SELECT MAX(id) FROM @tests), @stmt nvarchar(400), @expect varchar(6), @test varchar(60), @err nvarchar(2000), @ck nvarchar(100);

BEGIN TRAN;
WHILE @id <= @max
BEGIN
    SELECT @stmt = stmt, @expect = expect, @test = test FROM @tests WHERE id = @id;
    SET @err = NULL;
    BEGIN TRY EXEC (@stmt); END TRY BEGIN CATCH SET @err = ERROR_MESSAGE(); END CATCH
    IF @err IS NULL
        INSERT @out VALUES (@id, @test, CASE @expect WHEN 'accept' THEN 'ok: accepted' ELSE 'FAIL: NOT REJECTED' END);
    ELSE
    BEGIN
        SET @ck = CASE WHEN @err LIKE '%CHECK constraint "%' THEN
            SUBSTRING(@err, CHARINDEX('CHECK constraint "', @err) + 18,
                      CHARINDEX('"', @err, CHARINDEX('CHECK constraint "', @err) + 18) - CHARINDEX('CHECK constraint "', @err) - 18)
            END;
        INSERT @out VALUES (@id, @test,
            CASE WHEN @expect = 'reject' AND @ck IS NOT NULL THEN 'ok: rejected by ' + @ck
                 ELSE 'FAIL: unexpected error: ' + LEFT(@err, 80) END);
    END
    SET @id += 1;
END
ROLLBACK;

SELECT test, outcome FROM @out ORDER BY id;
SELECT COUNT(*) AS failures FROM @out WHERE outcome LIKE 'FAIL%';
