CREATE TABLE [pw].[User] (
    -- Core User Fields
    [UserId]             VARCHAR(100)    NOT NULL,
    [Email]              NVARCHAR(320)   NOT NULL,
    [Name]              NVARCHAR(500)   NOT NULL,

    CONSTRAINT [PK_pw_User] PRIMARY KEY ([UserId] ASC)
);
