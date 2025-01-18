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

CREATE TABLE "W3CLogs" (
	"Id"	INTEGER,
	"Date"	TEXT,
	"Time"	TEXT,
	"s-sitename"	TEXT,
	"s-computername"	TEXT,
	"s-ip"	TEXT,
	"cs-method"	TEXT,
	"cs-uri-stem"	TEXT,
	"cs-uri-query"	TEXT,
	"s-port"	INTEGER,
	"cs-username"	TEXT,
	"c-ip"	TEXT,
	"cs-version"	TEXT,
	"cs(User-Agent)"	TEXT,
	"cs(Cookie)"	TEXT,
	"cs(Referrer)"	TEXT,
	"cs-host"	TEXT,
	"sc-status"	INTEGER,
	"sc-substatus"	INTEGER,
	"sc-win32-status"	TEXT,
	"sc-bytes"	TEXT,
	"cs-bytes"	TEXT,
	"time-taken"	TEXT,
	"streamid"	TEXT,
	PRIMARY KEY("Id" AUTOINCREMENT)
)