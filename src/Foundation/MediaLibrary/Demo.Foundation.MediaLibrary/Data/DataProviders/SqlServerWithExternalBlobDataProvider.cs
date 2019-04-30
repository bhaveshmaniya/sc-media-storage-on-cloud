using Demo.Foundation.MediaLibrary.Interfaces;
using Microsoft.WindowsAzure.Storage;
using Sitecore.Collections;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.DataProviders;
using Sitecore.Data.Managers;
using Sitecore.Data.SqlServer;
using Sitecore.Diagnostics;
using Sitecore.Reflection;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Web;

namespace Demo.Foundation.MediaLibrary.Data.DataProviders
{
    public class SqlServerWithExternalBlobDataProvider : SqlServerDataProvider
    {
        private readonly LockSet _blobSetLocks;
        private readonly IBlobStorageProvider _blobStorageProvider;
        private readonly bool _externalBlobStorageProviderConfigured;

        public SqlServerWithExternalBlobDataProvider(string connectionString) : base(connectionString)
        {
            _blobSetLocks = new LockSet();

            var externalBlobStorageProviderType = Configuration.Settings.Media.ExternalBlobStorageProviderType;
            if (string.IsNullOrWhiteSpace(externalBlobStorageProviderType))
            {
                Log.Info("ExternalBlobStorageProviderType isn't configured. So, using the Sitecore default provider.", this);
                _externalBlobStorageProviderConfigured = false;
                return;
            }

            try
            {
                Log.Info($"Initializing ExternalBlobStorageProviderType using {externalBlobStorageProviderType}", this);

                _blobStorageProvider = ReflectionUtil.CreateObject(externalBlobStorageProviderType) as IBlobStorageProvider;
                if (_blobStorageProvider == null)
                {
                    Log.Error($"Unable to create IBlobStorageProvider of type {externalBlobStorageProviderType}. So, using the Sitecore default provider.", this);
                    _externalBlobStorageProviderConfigured = false;
                    return;
                }

                _externalBlobStorageProviderConfigured = true;
            }
            catch (Exception ex)
            {
                Log.Error($"Unable to initialize ExternalBlobStorageProviderType {externalBlobStorageProviderType}. So, using the Sitecore default provider.", ex, this);
                _externalBlobStorageProviderConfigured = false;
            }
        }

        public override Stream GetBlobStream(Guid blobId, CallContext context)
        {
            Assert.ArgumentNotNull(context, "context");

            if (!_externalBlobStorageProviderConfigured)
            {
                return base.GetBlobStream(blobId, context);
            }

            var memoryStream = new MemoryStream();
            _blobStorageProvider.Get(memoryStream, blobId.ToString());

            // Note: If blob stream not found from the external storage then fall-back to default one
            return memoryStream.Length > 0 ? memoryStream : base.GetBlobStream(blobId, context);
        }

        public override bool BlobStreamExists(Guid blobId, CallContext context)
        {
            Assert.ArgumentNotNull(context, "context");

            if (!_externalBlobStorageProviderConfigured)
            {
                return base.BlobStreamExists(blobId, context);
            }

            // Note: If blob stream not found from the external storage then fall-back to default one
            return _blobStorageProvider.Exists(blobId.ToString()) ? true : base.BlobStreamExists(blobId, context);
        }

        public override bool RemoveBlobStream(Guid blobId, CallContext context)
        {
            Assert.ArgumentNotNull(context, "context");

            if (!_externalBlobStorageProviderConfigured)
            {
                return base.RemoveBlobStream(blobId, context);
            }

            return _blobStorageProvider.Delete(blobId.ToString());
        }

        public override bool SetBlobStream(Stream stream, Guid blobId, CallContext context)
        {
            Assert.ArgumentNotNull(stream, "stream");
            Assert.ArgumentNotNull(context, "context");

            if (!_externalBlobStorageProviderConfigured)
            {
                return base.SetBlobStream(stream, blobId, context);
            }

            lock (_blobSetLocks.GetLock(blobId))
            {
                try
                {
                    _blobStorageProvider.Put(stream, blobId.ToString());

                    // Note: We should insert an empty reference to the BlobId into the SQL Blobs table, this is basically to assist with the cleanup process.
                    //       During cleanup, it's faster to query the database for the blobs that should be removed as opposed to retrieving and parsing a list from Azure.
                    string cmdText = " INSERT INTO [Blobs]( [Id], [BlobId], [Index], [Created], [Data] ) VALUES(   NewId(), @blobId, @index, @created, @data)";
                    using (var connection = new SqlConnection(Api.ConnectionString))
                    {
                        connection.Open();
                        var command = new SqlCommand(cmdText, connection)
                        {
                            CommandTimeout = (int)CommandTimeout.TotalSeconds
                        };
                        command.Parameters.AddWithValue("@blobId", blobId);
                        command.Parameters.AddWithValue("@index", 0);
                        command.Parameters.AddWithValue("@created", DateTime.UtcNow);
                        command.Parameters.Add("@data", SqlDbType.Image, 0).Value = new byte[0];
                        command.ExecuteNonQuery();
                    }
                }
                catch (StorageException ex)
                {
                    Log.Error($"AzureStorage: Upload of blob with Id {blobId} failed.", ex, this);
                    throw;
                }
            }

            return true;
        }

        protected override void CleanupBlobs(CallContext context)
        {
            Assert.ArgumentNotNull(context, "context");

            if (!_externalBlobStorageProviderConfigured)
            {
                base.CleanupBlobs(context);
                return;
            }

            Factory.GetRetryer().ExecuteNoResult(() => DoCleanup(context));
        }

        protected virtual void DoCleanup(CallContext context)
        {
            IEnumerable<Guid> blobsToDelete;

            using (var transaction = Api.CreateTransaction())
            {
                string blobsInUseTempTableName = "#BlobsInUse";
                Api.Execute("CREATE TABLE {0}" + blobsInUseTempTableName + "{1} ({0}ID{1} {0}uniqueidentifier{1})", new object[0]);

                var blobsInUse = GetBlobsInUse(context.DataManager.Database);

                foreach (var blobReference in blobsInUse)
                {
                    Api.Execute("INSERT INTO {0}" + blobsInUseTempTableName + "{1} VALUES ({2}id{3})", new object[] { "id", blobReference });
                }

                blobsToDelete = GetUnusedBlobs("#BlobsInUse");

                string sql = " DELETE\r\n FROM {0}Blobs{1}\r\n WHERE {0}BlobId{1} NOT IN (SELECT {0}ID{1} FROM {0}" + blobsInUseTempTableName + "{1})";
                Api.Execute(sql, new object[0]);
                Api.Execute("DROP TABLE {0}" + blobsInUseTempTableName + "{1}", new object[0]);
                transaction.Complete();
            }

            foreach (var blobId in blobsToDelete)
            {
                var success = _blobStorageProvider.Delete(blobId.ToString());
            }
        }

        //Note: Items in the recycle bin are technically still referenced/in use, so be sure to empty the recycle bin before attempting clean up blobs
        protected virtual IEnumerable<Guid> GetBlobsInUse(Database database)
        {
            var tables = new[] { "SharedFields", "UnversionedFields", "VersionedFields", "ArchivedFields" };
            var blobsInUse = new List<Guid>();

            foreach (var template in TemplateManager.GetTemplates(database).Values)
            {
                foreach (var field in template.GetFields())
                {
                    if (!field.IsBlob)
                        continue;

                    foreach (var sql in tables.Select(table => "SELECT DISTINCT {0}Value{1}\r\n FROM {0}" + table + "{1}\r\n WHERE {0}FieldId{1} = {2}fieldId{3}\r\n AND {0}Value{1} IS NOT NULL \r\n AND {0}Value{1} != {6}"))
                    {
                        using (var reader = Api.CreateReader(sql, new object[] { "fieldId", field.ID }))
                        {
                            while (reader.Read())
                            {
                                var id = Api.GetString(0, reader);
                                if (id.Length > 38)
                                {
                                    id = id.Substring(0, 38);
                                }
                                if (ID.TryParse(id, out ID parsedId))
                                {
                                    blobsInUse.Add(parsedId.Guid);
                                }
                            }
                        }
                    }
                }
            }

            return blobsInUse;
        }

        protected virtual IEnumerable<Guid> GetUnusedBlobs(string blobsInUseTempTableName)
        {
            var unusedBlobs = new List<Guid>();

            //This database call is dependent on the #BlobsInUse temporary table
            var sql = "SELECT {0}BlobId{1}\r\n FROM {0}Blobs{1}\r\b WHERE {0}BlobId{1} NOT IN (SELECT {0}ID{1} FROM {0}" + blobsInUseTempTableName + "{1})";
            using (var reader = Api.CreateReader(sql, new object[0]))
            {
                while (reader.Read())
                {
                    var id = Api.GetGuid(0, reader);
                    unusedBlobs.Add(id);
                }
            }

            return unusedBlobs;
        }
    }
}