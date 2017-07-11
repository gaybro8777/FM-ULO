//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace GSA.UnliquidatedObligations.BusinessLayer.Data
{
    using System;
    using System.Collections.Generic;
    
    public partial class WorkflowHistory
    {
        public int WorkflowHistoryId { get; set; }
        public int WorkflowId { get; set; }
        public string WorkflowKey { get; set; }
        public int Version { get; set; }
        public string CurrentWorkflowActivityKey { get; set; }
        public string OwnerUserId { get; set; }
        public System.DateTime CreatedAtUtc { get; set; }
        public System.DateTime CurrentActivityEnteredAtUtc { get; set; }
        public int TargetUloId { get; set; }
        public byte[] WorkflowRowVersion { get; set; }
        public System.DateTime CurrentActivityExitedAtUtc { get; set; }
    
        public virtual AspNetUser AspNetUser { get; set; }
        public virtual Workflow Workflow { get; set; }
    }
}
