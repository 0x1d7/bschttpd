CREATE TABLE "ExceptionLogs" (
	"Id"	INTEGER,
	"LogLevel"	TEXT,
	"Timestamp"	TEXT,
	"Message"	TEXT,
	PRIMARY KEY("Id" AUTOINCREMENT)
)

CREATE TABLE "StatusLogs" (
	"Id"	INTEGER,
	"Timestamp"	TEXT,
	"Message"	TEXT,
	PRIMARY KEY("Id" AUTOINCREMENT)
)