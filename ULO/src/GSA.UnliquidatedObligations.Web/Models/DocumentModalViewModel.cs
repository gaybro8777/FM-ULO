﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using GSA.UnliquidatedObligations.BusinessLayer.Data;
using System.Web.Mvc;

namespace GSA.UnliquidatedObligations.Web.Models
{
    public class DocumentModalViewModel
    {
        public int DocumentId { get; set; }
        public int? DocumentTypeId { get; set; }
        public List<SelectListItem> DocumentTypes { get; set; }

        public AttachmentsViewModel AttachmentsViewModel { get; set; }

        public DocumentModalViewModel()
        {

        }

        public DocumentModalViewModel(int documentId, int documentTypeId, List<DocumentType> documentTypes, List<Attachment> attachments )
        {
            DocumentId = documentId;
            DocumentTypeId = documentTypeId;
            DocumentTypes = ConvertToSelectList(documentTypes);
            AttachmentsViewModel = new AttachmentsViewModel(attachments, documentId);
        }

        private List<SelectListItem> ConvertToSelectList(List<DocumentType> documentTypes)
        {
            return documentTypes
                .Select(dt => new SelectListItem
                {
                    Text = dt.Name,
                    Value = dt.DocumentTypeId.ToString()
                }).ToList();
        }

    }

    public class AttachmentsViewModel
    {
        public List<Attachment> Attachments { get; set; }
        public int DocumentId { get; set; }
        public AttachmentsViewModel()
        {
            
        }

        public AttachmentsViewModel(List<Attachment> attachments, int documentId)
        {
            Attachments = attachments;
            DocumentId = documentId;
        }
    }

    public class UploadViewModel
    {
        public int DocumentIdForUpload { get; set; }

        public UploadViewModel()
        {
            
        }
    }
}