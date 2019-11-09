﻿using System;
using System.Data;

namespace GSA.UnliquidatedObligations.Utility
{
    public class LoadRowsSettings
    {
        public Action<Exception, int> RowAddErrorHandler { get; set; }
        public string RowNumberColumnName { get; set; }
        public Func<DataTable, string, string> DuplicateColumnRenamer { get; set; }
        public Func<string, string> ColumnMapper { get; set; }
        public Func<object, Type, object> TypeConverter { get; set; }
        public Func<DataTable, object[], bool> ShouldAddRow { get; set; }

        public LoadRowsSettings()
        {
            TypeConverter = Convert.ChangeType;
            ShouldAddRow = DataTableHelpers.DontAddEmptyRows;
        }

        public LoadRowsSettings(LoadRowsSettings other)
            : this()
        {
            if (other == null) return;
            this.RowAddErrorHandler = other.RowAddErrorHandler;
            this.RowNumberColumnName = other.RowNumberColumnName;
            this.DuplicateColumnRenamer = other.DuplicateColumnRenamer;
            this.ColumnMapper = other.ColumnMapper;
            this.TypeConverter = other.TypeConverter;
        }
    }
}
