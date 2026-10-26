using Npgsql;

namespace Planarian.Tests;

/// <summary>
/// Purpose-specific SQL fixture for the exact current-main v32 schema. The current EF model
/// intentionally differs from v32, so migration-arrangement state must not be created through it.
/// Every durable v32 model table receives at least one row so the preservation census cannot
/// silently omit an unrelated subsystem.
/// </summary>
internal static class CurrentMainV32CensusSeeder
{
    internal const string AccountAlpha = "acctalpha1";
    internal const string UserAlpha = "useralpha1";

    internal static readonly string[] SeededFeatureSettingIds =
        ["fsetglobal", "fsetbeta01", "fsetother1", "fsetempty1"];

    internal static async Task SeedCompleteDurableDatabaseAsync(PostgresTestDatabase database)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into "Users"
                ("Id","FirstName","LastName","EmailAddress","PendingEmailAddress","PhoneNumber","HashedPassword",
                 "PasswordResetCode","EmailConfirmationCode","EmailConfirmedOn","SessionVersion","IsTemporary",
                 "PasswordResetCodeExpiration","ProfilePhotoBlobKey","LastActiveOn","CreatedOn")
            values
                ('useralpha1','Alpha','User','alpha@example.test','alpha.pending@example.test','6155550101',
                 'legacy-hash-alpha','reset-alpha','confirm-a','2026-01-02T03:04:05Z',7,false,
                 '2026-02-02T03:04:05Z','profiles/alpha.jpg','2026-10-01T12:00:00Z','2024-01-01T00:00:00Z'),
                ('userbeta01','Beta','User','beta@example.test',null,'6155550102',
                 'legacy-hash-beta',null,null,null,3,false,null,null,'2026-09-30T12:00:00Z','2024-01-02T00:00:00Z'),
                ('userinvite','Invited','User','invite@example.test',null,null,null,null,null,null,0,true,
                 null,null,null,'2024-01-03T00:00:00Z');

            insert into "States"
                ("Id","Name","Abbreviation","CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values
                ('state00001','Tennessee','TN','useralpha1','userbeta01','2024-01-04T00:00:00Z','2024-06-04T00:00:00Z'),
                ('state00002','Kentucky','KY','userbeta01',null,'2024-01-05T00:00:00Z',null);

            insert into "Accounts"
                ("Id","Name","CountyIdDelimiter","DefaultViewAccessAllCaves","ExportEnabled",
                 "CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values
                ('acctalpha1','Alpha Survey','-',false,true,'useralpha1','userbeta01','2024-01-06T00:00:00Z','2024-07-06T00:00:00Z'),
                ('acctbeta01','Beta Survey','/',true,false,'userbeta01',null,'2024-01-07T00:00:00Z',null),
                ('acctempty1','No Cave Account','.',false,true,'useralpha1',null,'2024-01-08T00:00:00Z',null);

            insert into "AccountUsers"
                ("AccountId","UserId","Id","InvitationAcceptedOn","InvitationSentOn","InvitationCode",
                 "CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values
                ('acctalpha1','useralpha1','acctusr001','2024-02-01T00:00:00Z','2024-01-20T00:00:00Z',null,
                 'useralpha1','userbeta01','2024-01-20T00:00:00Z','2024-02-01T00:00:00Z'),
                ('acctalpha1','userinvite','acctusr002',null,'2026-09-01T00:00:00Z','invite0001',
                 'useralpha1',null,'2026-09-01T00:00:00Z',null),
                ('acctbeta01','useralpha1','acctusr003','2024-03-01T00:00:00Z','2024-02-15T00:00:00Z',null,
                 'userbeta01',null,'2024-02-15T00:00:00Z','2024-03-01T00:00:00Z'),
                ('acctbeta01','userbeta01','acctusr004','2024-03-02T00:00:00Z','2024-02-16T00:00:00Z',null,
                 'userbeta01',null,'2024-02-16T00:00:00Z','2024-03-02T00:00:00Z'),
                ('acctempty1','useralpha1','acctusr005','2024-04-01T00:00:00Z','2024-03-15T00:00:00Z',null,
                 'useralpha1',null,'2024-03-15T00:00:00Z','2024-04-01T00:00:00Z');

            insert into "AccountStates"
                ("AccountId","StateId","Id","CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values
                ('acctalpha1','state00001','astatealp1','useralpha1','userbeta01','2024-01-09T00:00:00Z','2024-05-09T00:00:00Z'),
                ('acctbeta01','state00002','astatebet1','userbeta01',null,'2024-01-10T00:00:00Z',null),
                ('acctempty1','state00001','astateemp1','useralpha1',null,'2024-01-11T00:00:00Z',null);

            insert into "Counties"
                ("Id","AccountId","StateId","DisplayId","Name","CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values
                ('cntyalpha1','acctalpha1','state00001','A01','Alpha County','useralpha1','userbeta01','2024-01-12T00:00:00Z','2024-05-12T00:00:00Z'),
                ('cntyalpha2','acctalpha1','state00001','A02','Archive County','useralpha1',null,'2024-01-13T00:00:00Z',null),
                ('cntybeta01','acctbeta01','state00002','B01','Beta County','userbeta01',null,'2024-01-14T00:00:00Z',null),
                ('cntyempty1','acctempty1','state00001','E01','Empty County','useralpha1',null,'2024-01-15T00:00:00Z',null);

            insert into "Permissions"
                ("Id","Name","Description","IsHidden","Key","SortOrder","PermissionType",
                 "CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values
                ('permview01','View','View caves',false,'View',0,'Cave','useralpha1','userbeta01','2024-01-16T00:00:00Z','2024-06-16T00:00:00Z'),
                ('permmgr001','Manager','Manage caves',false,'Manager',1,'Cave','useralpha1',null,'2024-01-17T00:00:00Z',null),
                ('permadmin1','Admin','Account administration',false,'Admin',2,'User','useralpha1',null,'2024-01-18T00:00:00Z',null);

            insert into "UserPermissions"
                ("Id","UserId","PermissionId","AccountId","CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values
                ('upermadmin','useralpha1','permadmin1','acctalpha1','useralpha1','userbeta01','2024-01-19T00:00:00Z','2024-07-19T00:00:00Z'),
                ('upermglob1','useralpha1','permadmin1',null,'useralpha1',null,'2024-01-19T01:00:00Z',null),
                ('upermview1','userbeta01','permview01','acctbeta01','userbeta01',null,'2024-01-20T00:00:00Z',null);

            insert into "Projects"
                ("Id","Name","CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values ('project001','Unrelated Survey Project','useralpha1','userbeta01','2024-01-21T00:00:00Z','2024-08-21T00:00:00Z');

            insert into "TagTypes"
                ("Id","ProjectId","AccountId","Key","Name","IsDefault","CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values
                ('tagfile001',null,'acctalpha1','File','Document',false,'useralpha1','userbeta01','2024-01-22T00:00:00Z','2024-07-22T00:00:00Z'),
                ('taglocq001',null,'acctalpha1','LocationQuality','Survey Grade',true,'useralpha1',null,'2024-01-23T00:00:00Z',null),
                ('taggeo0001',null,'acctalpha1','Geology','Limestone',false,'useralpha1',null,'2024-01-24T00:00:00Z',null),
                ('tagage0001',null,'acctalpha1','GeologicAge','Mississippian',false,'useralpha1',null,'2024-01-25T00:00:00Z',null),
                ('tagmap0001',null,'acctalpha1','MapStatus','Mapped',false,'useralpha1',null,'2024-01-26T00:00:00Z',null),
                ('tagphys001',null,'acctalpha1','PhysiographicProvince','Highland Rim',false,'useralpha1',null,'2024-01-27T00:00:00Z',null),
                ('tagarch001',null,'acctalpha1','Archeology','Artifact',false,'useralpha1',null,'2024-01-28T00:00:00Z',null),
                ('tagbio0001',null,'acctalpha1','Biology','Bats',false,'useralpha1',null,'2024-01-29T00:00:00Z',null),
                ('tagpeople1',null,'acctalpha1','People','Alice Cartographer',false,'useralpha1',null,'2024-01-30T00:00:00Z',null),
                ('tagother01',null,'acctalpha1','CaveOther','Restricted',false,'useralpha1',null,'2024-01-31T00:00:00Z',null),
                ('tagstat001',null,'acctalpha1','EntranceStatus','Open',false,'useralpha1',null,'2024-02-01T00:00:00Z',null),
                ('taghydr001',null,'acctalpha1','EntranceHydrology','Wet',false,'useralpha1',null,'2024-02-02T00:00:00Z',null),
                ('tagfield01',null,'acctalpha1','FieldIndication','Sinkhole',false,'useralpha1',null,'2024-02-03T00:00:00Z',null),
                ('tagfile002',null,'acctbeta01','File','Beta Document',false,'userbeta01',null,'2024-02-04T00:00:00Z',null),
                ('taglocq002',null,'acctbeta01','LocationQuality','GPS',false,'userbeta01',null,'2024-02-05T00:00:00Z',null),
                ('tagproj001','project001',null,'Trip','Survey Trip',false,'useralpha1',null,'2024-02-06T00:00:00Z',null),
                ('taglead001','project001',null,'Lead','High Priority',false,'useralpha1',null,'2024-02-07T00:00:00Z',null);

            insert into "Trips"
                ("Id","ProjectId","Name","Description","TripReport","TagTypeId",
                 "CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values ('trip000001','project001','Unrelated Trip','Trip description','Trip report body','tagproj001',
                    'useralpha1','userbeta01','2024-02-08T00:00:00Z','2024-08-08T00:00:00Z');

            insert into "Members"
                ("Id","ProjectId","TripId","UserId","CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values
                ('member0001','project001',null,'useralpha1','useralpha1','userbeta01','2024-02-09T00:00:00Z','2024-08-09T00:00:00Z'),
                ('member0002',null,'trip000001','userbeta01','useralpha1',null,'2024-02-10T00:00:00Z',null);

            insert into "Photos"
                ("Id","TripId","Name","Description","FileType","BlobKey","CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values ('photo00001','trip000001','Entrance Photo','Unrelated photo metadata','jpg','photos/unrelated.jpg',
                    'useralpha1','userbeta01','2024-02-11T00:00:00Z','2024-08-11T00:00:00Z');

            insert into "Leads"
                ("Id","TripId","Description","ClosestStation","Classification","DateClosed",
                 "CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values ('lead000001','trip000001','Unrelated lead','A12','High','2025-03-04T00:00:00Z',
                    'useralpha1','userbeta01','2024-02-12T00:00:00Z','2025-03-04T00:00:00Z');

            insert into "TripTags"("TagTypeId","TripId","Id","CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values ('tagproj001','trip000001','triptag001','useralpha1','userbeta01','2024-02-13T00:00:00Z','2024-08-13T00:00:00Z');

            insert into "LeadTags"("TagTypeId","LeadId","Id","CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values ('taglead001','lead000001','leadtag001','useralpha1','userbeta01','2024-02-14T00:00:00Z','2024-08-14T00:00:00Z');

            insert into "Caves"
                ("Id","AccountId","StateId","CountyId","Name","AlternateNames","CountyNumber","LengthFeet","DepthFeet",
                 "MaxPitDepthFeet","NumberOfPits","Narrative","ReportedOn","ReportedByUserId","IsArchived",
                 "CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values
                ('cavealpha1','acctalpha1','state00001','cntyalpha1','Alpha Cave','["Alpha Cavern","Old Alpha"]',101,
                 1234.5,210.25,86.75,7,'Rich narrative with apostrophe: ''preserved'' and unicode Ω.',
                 '2020-05-06T07:08:09Z','useralpha1',false,'useralpha1','userbeta01','2021-01-01T00:00:00Z','2026-01-01T00:00:00Z'),
                ('cavealpha2','acctalpha1','state00001','cntyalpha2','Archived Alpha','["Old Archive"]',7,
                 null,null,null,null,null,null,null,true,'useralpha1',null,'2021-02-01T00:00:00Z',null),
                ('cavebeta01','acctbeta01','state00002','cntybeta01','Beta Cave','[]',3,
                 0,0,0,0,'Beta narrative','2022-03-04T00:00:00Z','userbeta01',false,
                 'userbeta01',null,'2022-03-01T00:00:00Z',null);

            insert into "CavePermission"
                ("Id","UserId","AccountId","PermissionId","StateId","CountyId","CaveId",
                 "CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values
                ('cpermacc01','userinvite','acctalpha1','permview01',null,null,null,'useralpha1',null,'2024-02-15T00:00:00Z',null),
                ('cpermst001','useralpha1','acctalpha1','permmgr001','state00001',null,null,'useralpha1','userbeta01','2024-02-16T00:00:00Z','2024-08-16T00:00:00Z'),
                ('cpermcnty1','userbeta01','acctalpha1','permview01',null,'cntyalpha1',null,'useralpha1',null,'2024-02-17T00:00:00Z',null),
                ('cpermcave1','userbeta01','acctalpha1','permmgr001',null,null,'cavealpha1','useralpha1',null,'2024-02-18T00:00:00Z',null);

            insert into "FeatureSettings"
                ("Id","AccountId","Key","IsEnabled","IsDefault","CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values
                ('fsetglobal',null,'EnabledFieldEntranceOtherTags',true,true,'useralpha1','userbeta01','2024-02-19T00:00:00Z','2024-08-19T00:00:00Z'),
                ('fsetbeta01','acctbeta01','EnabledFieldEntranceOtherTags',false,false,'userbeta01',null,'2024-02-20T00:00:00Z',null),
                ('fsetother1','acctalpha1','EnabledFieldCaveName',false,false,'useralpha1','userbeta01','2024-02-21T00:00:00Z','2024-08-21T00:00:00Z'),
                ('fsetempty1','acctempty1','EnabledFieldCaveName',true,false,'useralpha1',null,'2024-02-22T00:00:00Z',null);

            insert into "Entrances"
                ("Id","CaveId","LocationQualityTagId","Name","IsPrimary","Description","Location","ReportedOn",
                 "ReportedByUserId","PitDepthFeet","CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values
                ('entalpha01','cavealpha1','taglocq001','Main Entrance',true,'Primary entrance',
                 ST_SetSRID(ST_MakePoint(-86.7654,36.1234,612.25),4326),'2020-05-06T07:08:09Z','useralpha1',42.5,
                 'useralpha1','userbeta01','2021-01-01T01:00:00Z','2026-01-01T01:00:00Z'),
                ('entalpha02','cavealpha1','taglocq001',null,false,null,
                 ST_SetSRID(ST_MakePoint(0,0,0),4326),null,null,0,
                 'useralpha1',null,'2021-01-01T02:00:00Z',null),
                ('entbeta001','cavebeta01','taglocq002','Beta Entrance',true,'Beta entrance',
                 ST_SetSRID(ST_MakePoint(-85.5,37.5,450),4326),'2022-03-04T00:00:00Z','userbeta01',null,
                 'userbeta01',null,'2022-03-01T01:00:00Z',null);

            insert into "GeologyTags"("TagTypeId","CaveId","Id","CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values ('taggeo0001','cavealpha1','relgeo0001','useralpha1','userbeta01','2024-03-01T00:00:00Z','2024-09-01T00:00:00Z');
            insert into "GeologicAgeTags"("TagTypeId","CaveId","Id","CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values ('tagage0001','cavealpha1','relage0001','useralpha1',null,'2024-03-02T00:00:00Z',null);
            insert into "MapStatusTags"("TagTypeId","CaveId","Id","CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values ('tagmap0001','cavealpha1','relmap0001','useralpha1',null,'2024-03-03T00:00:00Z',null);
            insert into "PhysiographicProvinceTags"("TagTypeId","CaveId","Id","CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values ('tagphys001','cavealpha1','relphys001','useralpha1',null,'2024-03-04T00:00:00Z',null);
            insert into "ArcheologyTags"("TagTypeId","CaveId","Id","CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values ('tagarch001','cavealpha1','relarch001','useralpha1',null,'2024-03-05T00:00:00Z',null);
            insert into "BiologyTags"("TagTypeId","CaveId","Id","CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values ('tagbio0001','cavealpha1','relbio0001','useralpha1',null,'2024-03-06T00:00:00Z',null);
            insert into "CaveOtherTags"("TagTypeId","CaveId","Id","CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values ('tagother01','cavealpha1','relother01','useralpha1',null,'2024-03-07T00:00:00Z',null);
            insert into "CartographerNameTags"("TagTypeId","CaveId","Id","CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values ('tagpeople1','cavealpha1','relcart001','useralpha1',null,'2024-03-08T00:00:00Z',null);
            insert into "CaveReportedByNameTags"("TagTypeId","CaveId","Id","CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values ('tagpeople1','cavealpha1','relrepr001','useralpha1',null,'2024-03-09T00:00:00Z',null);

            insert into "EntranceStatusTags"("TagTypeId","EntranceId","Id","CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values ('tagstat001','entalpha01','relstat001','useralpha1','userbeta01','2024-03-10T00:00:00Z','2024-09-10T00:00:00Z');
            insert into "EntranceHydrologyTags"("TagTypeId","EntranceId","Id","CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values ('taghydr001','entalpha01','relhydr001','useralpha1',null,'2024-03-11T00:00:00Z',null);
            insert into "FieldIndicationTags"("TagTypeId","EntranceId","Id","CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values ('tagfield01','entalpha01','relfield01','useralpha1',null,'2024-03-12T00:00:00Z',null);
            insert into "EntranceReportedByNameTags"("TagTypeId","EntranceId","Id","CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values ('tagpeople1','entalpha01','relerep001','useralpha1',null,'2024-03-13T00:00:00Z',null);
            insert into "EntranceOtherTag"("TagTypeId","EntranceId","Id","CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values ('tagother01','entalpha01','releoth001','useralpha1',null,'2024-03-14T00:00:00Z',null);

            insert into "Files"
                ("Id","AccountId","CaveId","FileTypeTagId","FileName","DisplayName","BlobKey","BlobContainer","ExpiresOn",
                 "CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values
                ('filealpha1','acctalpha1','cavealpha1','tagfile001','survey.v1.pdf','Survey Map',
                 'caves/alpha/survey-v1.pdf','account-alpha',null,'useralpha1','userbeta01','2024-04-01T00:00:00Z','2024-09-01T00:00:00Z'),
                ('filealpha2',null,'cavealpha1','tagfile001','photos.tar.gz',null,
                 'caves/alpha/photos.tar.gz','account-alpha',null,'useralpha1',null,'2024-04-02T00:00:00Z',null),
                ('filealpha3','acctalpha1',null,'tagfile001','temporary.csv',null,
                 'staging/temporary.csv','account-alpha','2026-12-31T23:59:59Z','useralpha1',null,'2024-04-03T00:00:00Z',null),
                ('filealpha4','acctalpha1','cavealpha2','tagfile001','README','Field Notes',
                 'caves/archive/readme','account-alpha',null,'useralpha1',null,'2024-04-04T00:00:00Z',null),
                ('filebeta01','acctbeta01','cavebeta01','tagfile002','beta.gpx','beta',
                 'caves/beta/beta.gpx','account-beta',null,'userbeta01',null,'2024-04-05T00:00:00Z',null);

            insert into "CaveGeoJsons"
                ("Id","CaveId","Name","GeoJson","CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values
                ('linealpha1','cavealpha1','Alpha Centerline',
                 '{"type":"FeatureCollection","features":[{"type":"Feature","properties":{"kind":"centerline","order":1},"geometry":{"type":"LineString","coordinates":[[-86.7,36.1],[-86.6,36.2]]}}]}'::jsonb,
                 'useralpha1','userbeta01','2024-04-06T00:00:00Z','2024-09-06T00:00:00Z'),
                ('linealpha2','cavealpha1','Alpha Detail',
                 '{"type":"FeatureCollection","features":[]}'::jsonb,
                 'useralpha1',null,'2024-04-07T00:00:00Z',null),
                ('linebeta01','cavebeta01','Beta Plot',
                 '{"type":"FeatureCollection","features":[{"type":"Feature","properties":{},"geometry":{"type":"Point","coordinates":[-85.5,37.5]}}]}'::jsonb,
                 'userbeta01',null,'2024-04-08T00:00:00Z',null);

            insert into "Favorites"
                ("Id","UserId","AccountId","CaveId","CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values
                ('favalpha01','useralpha1','acctalpha1','cavealpha1','useralpha1','userbeta01','2024-04-09T00:00:00Z','2024-09-09T00:00:00Z'),
                ('favlegacy1','userbeta01',null,'cavealpha2','userbeta01',null,'2024-04-10T00:00:00Z',null);

            insert into "MessageTypes"
                ("Id","Key","Type","Description","IsEnabled","Subject","FromName","FromEmail","Mjml","Html",
                 "CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values ('msgtype001','AccountInvitation','Email','Invitation template',true,'Invitation','Planarian','noreply@example.test',
                    '<mjml>invite</mjml>','<html>invite</html>','useralpha1','userbeta01','2024-04-11T00:00:00Z','2024-09-11T00:00:00Z');

            insert into "MessageLogs"
                ("Id","MessageKey","MessageType","Purpose","Provider","ProviderDomain","ProviderCorrelationId","ProviderMessageId",
                 "DeliveryStatus","DeliveryStatusOn","DeliveryStatusMessage","Subject","ToEmailAddress","ToName","FromName",
                 "FromEmailAddress","Substitutions","AccountInvitationAccountId","AccountInvitationUserId",
                 "CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values
                ('msginvite1','AccountInvitation','Email','AccountInvitation','Mailgun','mg.example.test','corr000001','provider-message-1',
                 'Delivered','2026-09-01T00:05:00Z','Delivered successfully','Invitation','invite@example.test','Invited User',
                 'Planarian','noreply@example.test','{"name":"Invited"}','acctalpha1','userinvite',
                 'useralpha1','userbeta01','2026-09-01T00:00:00Z','2026-09-01T00:05:00Z'),
                ('msgconf001','EmailConfirmation','Email','EmailConfirmation','Mailgun','mg.example.test','corr000002','provider-message-2',
                 'Accepted','2026-01-02T03:05:00Z',null,'Confirm','alpha@example.test','Alpha User',
                 'Planarian','noreply@example.test','{"code":"confirm-a"}',null,null,
                 'useralpha1',null,'2026-01-02T03:04:00Z',null);

            update "Users"
            set "EmailConfirmationMessageLogId"='msgconf001'
            where "Id"='useralpha1';

            insert into "MessageLogEvents"
                ("Id","MessageLogId","Provider","ProviderDomain","EventType","OccurredOn","ProviderEventType","ProviderEventId",
                 "ProviderEventDay","WebhookToken","Severity","Reason","DeliveryCode","EnhancedDeliveryCode","DeliveryMessage",
                 "AttemptNumber","IsDelayedBounce","Bot","CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values ('msgevent01','msginvite1','Mailgun','mg.example.test','Delivered','2026-09-01T00:05:00Z','delivered','evt-001',
                    '2026-09-01','webhook-001','info',null,'250','2.0.0','Delivered',1,false,'false',
                    'useralpha1','userbeta01','2026-09-01T00:05:00Z','2026-09-01T00:06:00Z');

            insert into "ThrottleEventLogs"
                ("Id","OperationName","LimiterKeyType","Path","UserId","AccountId","IpAddress","NormalizedIdentifier",
                 "Limit","WindowSeconds","RetryAfterSeconds","OccurredOn","CreatedByUserId","ModifiedByUserId","CreatedOn","ModifiedOn")
            values ('throttle01','Login','LoginEmail','/api/login','useralpha1','acctalpha1','127.0.0.1','alpha@example.test',
                    5,60,12,'2026-09-02T00:00:00Z','useralpha1','userbeta01','2026-09-02T00:00:00Z','2026-09-02T00:01:00Z');
            """;
        await command.ExecuteNonQueryAsync();
    }
}
