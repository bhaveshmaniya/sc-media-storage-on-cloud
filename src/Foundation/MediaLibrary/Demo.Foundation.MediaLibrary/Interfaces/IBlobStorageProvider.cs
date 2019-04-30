using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace Demo.Foundation.MediaLibrary.Interfaces
{
    public interface IBlobStorageProvider
    {
        bool Delete(string blobId);
        bool Exists(string blobId);
        void Get(Stream target, string blobId);
        void Put(Stream stream, string blobId);
        void DownloadToFile(string blobId, string filePath);
    }
}