-- Proves the CHECK constraints (Add{Range,AllowedValue,CrossColumn,NotBlank}CheckConstraints) reject bad data.
-- Each test runs one invalid UPDATE against an existing row; the whole run is rolled back, so nothing changes.
-- Run:  sqlcmd -S .\SQLEXPRESS -d QuloobulMoeMieneen -E -C -W -i Data/CheckConstraintTests.sql
-- Expect: every "expect" = reject row shows the constraint that stopped it, and every accept (control) row
-- shows "accepted". Any "NOT REJECTED" / "REJECTED (unexpected)" line is a failure. Needs >= 1 row per table.
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON; -- sqlcmd defaults it off, and Event's filtered index refuses updates without it
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
 ('accept', 'Resource_Request.Status = ''Approved''',N'UPDATE TOP (1) [Resource_Request] SET Status = ''Approved'', ReviewedBy = (SELECT TOP (1) UserId FROM [User])'),
 ('reject', 'Resource_Request.Priority = ''Urgent''',N'UPDATE TOP (1) [Resource_Request] SET Priority = ''Urgent'''),
 ('reject', 'Request_Status_History = ''Bogus''',N'UPDATE TOP (1) [Request_Status_History] SET NewStatus = ''Bogus'''),
 ('reject', 'User.Status = ''Bogus''',           N'UPDATE TOP (1) [User] SET IsActive = 0, Status = ''Bogus'''),
 ('accept', 'User.Status = ''Locked''',          N'UPDATE TOP (1) [User] SET IsActive = 0, Status = ''Locked'''),
 ('reject', 'Project.Status = ''Bogus''',        N'UPDATE TOP (1) [Project] SET Status = ''Bogus'''),
 ('accept', 'Project.Status = ''Closed''',       N'UPDATE TOP (1) [Project] SET Status = ''Closed'''),
 ('reject', 'Task_Schedule.DayOfWeek = ''Funday''',  N'UPDATE TOP (1) [Task_Schedule] SET DayOfWeek = ''Funday'''),
 ('accept', 'Task_Schedule.DayOfWeek = ''Friday''',  N'UPDATE TOP (1) [Task_Schedule] SET DayOfWeek = ''Friday'''),
 -- cross-column rules (AddCrossColumnCheckConstraints); each sets both columns so no particular row is needed
 ('reject', 'User IsActive=0 but Status Active', N'UPDATE TOP (1) [User] SET IsActive = 0, Status = ''Active'''),
 ('reject', 'User IsActive=1 but Status Locked', N'UPDATE TOP (1) [User] SET IsActive = 1, Status = ''Locked'''),
 ('accept', 'User IsActive=0 and Status Locked', N'UPDATE TOP (1) [User] SET IsActive = 0, Status = ''Locked'''),
 ('reject', 'User Inactive without DeactivatedAt',N'UPDATE TOP (1) [User] SET IsActive = 0, Status = ''Inactive'', DeactivatedAt = NULL'),
 ('accept', 'User Inactive with DeactivatedAt',  N'UPDATE TOP (1) [User] SET IsActive = 0, Status = ''Inactive'', DeactivatedAt = SYSUTCDATETIME()'),
 ('reject', 'User token hash without expiry',    N'UPDATE TOP (1) [User] SET PasswordTokenHash = ''x'', PasswordTokenExpires = NULL'),
 ('accept', 'User token hash with expiry',       N'UPDATE TOP (1) [User] SET PasswordTokenHash = ''x'', PasswordTokenExpires = SYSUTCDATETIME()'),
 ('reject', 'Request Declined without reviewer', N'UPDATE TOP (1) [Resource_Request] SET Status = ''Declined'', ReviewedBy = NULL'),
 ('accept', 'Request Approved with reviewer',    N'UPDATE TOP (1) [Resource_Request] SET Status = ''Approved'', ReviewedBy = (SELECT TOP (1) UserId FROM [User])'),
 ('reject', 'Task Done without CompletedBy',     N'UPDATE TOP (1) [Task] SET Status = ''Done'', CompletedBy = NULL'),
 ('accept', 'Task Done with CompletedBy',        N'UPDATE TOP (1) [Task] SET Status = ''Done'', CompletedBy = (SELECT TOP (1) UserId FROM [User])'),
 -- non-empty text rules (AddNotBlankCheckConstraints)
 ('reject', 'Project.ProjectName empty',         N'UPDATE TOP (1) [Project] SET ProjectName = '''''),
 ('accept', 'Project.ProjectName set',           N'UPDATE TOP (1) [Project] SET ProjectName = ''Test'''),
 ('reject', 'Task.Title spaces only',            N'UPDATE TOP (1) [Task] SET Title = ''   '''),
 ('accept', 'Task.Title set',                    N'UPDATE TOP (1) [Task] SET Title = ''Test'''),
 ('reject', 'Meeting.Title empty',               N'UPDATE TOP (1) [Meeting] SET Title = '''''),
 ('reject', 'Event.Title empty',                 N'UPDATE TOP (1) [Event] SET Title = '''''),
 ('reject', 'File_Upload.FileName empty',        N'UPDATE TOP (1) [File_Upload] SET FileName = '''''),
 ('reject', 'File_Upload.FilePath spaces only',  N'UPDATE TOP (1) [File_Upload] SET FilePath = ''   '''),
 ('accept', 'File_Upload name and path set',     N'UPDATE TOP (1) [File_Upload] SET FileName = ''a.pdf'', FilePath = ''c/a.pdf'''),
 ('reject', 'User.FirstName empty',              N'UPDATE TOP (1) [User] SET FirstName = '''''),
 ('reject', 'User.Surname spaces only',          N'UPDATE TOP (1) [User] SET Surname = ''  '''),
 ('accept', 'User names set',                    N'UPDATE TOP (1) [User] SET FirstName = ''A'', Surname = ''B'''),
 ('reject', 'User.Email no @',                   N'UPDATE TOP (1) [User] SET Email = ''not-an-email'''),
 ('reject', 'User.Email no dot after @',         N'UPDATE TOP (1) [User] SET Email = ''a@b'''),
 ('accept', 'User.Email well formed',            N'UPDATE TOP (1) [User] SET Email = ''a@b.co''');

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
