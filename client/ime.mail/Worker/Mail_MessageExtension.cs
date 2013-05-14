using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ime.mail.Net.Mail;
using ime.mail.Net.MIME;
using System.IO;

namespace ime.mail.Worker
{
    public static class Mail_MessageExtension
    {
        public static MIME_Entity CreateImage(this Mail_Message xml, string file)
        {
            if (file == null)
            {
                throw new ArgumentNullException("file");
            }
            string ext = Path.GetExtension(file);
            string media_type = null;
            if (".gif".Equals(ext, StringComparison.CurrentCultureIgnoreCase))
                media_type = MIME_MediaTypes.Image.gif;
            else if (".jpg".Equals(ext, StringComparison.CurrentCultureIgnoreCase) || ".jpeg".Equals(ext, StringComparison.CurrentCultureIgnoreCase))
                media_type = MIME_MediaTypes.Image.jpeg;
            else if (".tiff".Equals(ext, StringComparison.CurrentCultureIgnoreCase))
                media_type = MIME_MediaTypes.Image.tiff;
            else
                throw new Exception("file extension is error");

            MIME_Entity retVal = new MIME_Entity();
            MIME_b_Image body = new MIME_b_Image(media_type);
            retVal.Body = body;
            body.SetDataFromFile(file, MIME_TransferEncodings.Base64);
            retVal.ContentType.Param_Name = Path.GetFileName(file);

            FileInfo fileInfo = new FileInfo(file);
            MIME_h_ContentDisposition disposition = new MIME_h_ContentDisposition(MIME_DispositionTypes.Attachment);
            disposition.Param_FileName = Path.GetFileName(file);
            disposition.Param_Size = fileInfo.Length;
            disposition.Param_CreationDate = fileInfo.CreationTime;
            disposition.Param_ModificationDate = fileInfo.LastWriteTime;
            disposition.Param_ReadDate = fileInfo.LastAccessTime;
            retVal.ContentDisposition = disposition;

            return retVal;
        }
    }
}
