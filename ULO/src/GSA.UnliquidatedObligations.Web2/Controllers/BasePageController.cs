﻿using System;
using System.Collections.Generic;
using System.Linq;
using GSA.UnliquidatedObligations.BusinessLayer.Data;
using Microsoft.AspNetCore.Mvc;
using RevolutionaryStuff.Core;
using RevolutionaryStuff.Core.Caching;
using Serilog;
using Serilog.Core;
using Serilog.Core.Enrichers;

namespace GSA.UnliquidatedObligations.Web.Controllers
{
    public abstract class BasePageController : Controller
    {
        public static class WorkflowStalenessMagicFieldNames
        {
            public const string WorkflowRowVersionString = "StalenessWorkflowRowVersionString";
            public const string EditingBeganAtUtc = "StalenessEditingBeganAtUtc";
            public const string WorkflowId = "StalenessWorkflowId";
        }

        protected readonly PortalHelpers PortalHelpers;
        protected readonly ILogger Logger;
        protected readonly ICacher Cacher;
        protected readonly UloDbContext DB;
        protected readonly UserHelpers UserHelpers;

        protected BasePageController(UloDbContext db, ICacher cacher, PortalHelpers portalHelpers, UserHelpers userHelpers, ILogger logger)
        {
            Requires.NonNull(logger, nameof(logger));

            DB = db;

            Cacher = cacher;

            PortalHelpers = portalHelpers;
            UserHelpers = userHelpers;

            Logger = logger.ForContext(new ILogEventEnricher[]
            {
                new PropertyEnricher(typeof(Type).Name, this.GetType().Name),
            });

            Logger.Debug("Page request to {Controller}", this.GetType().Name);
        }

        public string CurrentUserId
            => UserHelpers.CurrentUserId;

        protected void OnlySupportedInDevelopmentEnvironment()
        {
            if (!PortalHelpers.UseDevAuthentication)
            {
                throw new NotSupportedException($"You are trying to make a call that is only allowed when {PortalHelpers}.{PortalHelpers.UseDevAuthentication}=true");
            } 
        }

        private int SetTotalItemCount<T>(IQueryable<T> q)
        {
            var cnt = q.Count();
            ViewBag.TotalItemCount = cnt;
            return cnt;
        }

        protected IQueryable<T> ApplyBrowse<T>(IQueryable<T> q, string sortCol, string sortDir, int? page, int? pageSize, IDictionary<string, string> colMapper = null)
        {
            var cnt = SetTotalItemCount(q);
            if (cnt == 0)
            {
                q = (new T[0]).AsQueryable();
            }
            else
            {
                q = ApplySort(q, sortCol, sortDir, colMapper);
                q = ApplyPagination(q, page, pageSize);
            }
            return q;
        }

        protected IQueryable<T> ApplyBrowse<T>(IQueryable<T> q, string sortCol, Type enumType, string sortDir, int? page, int? pageSize, IDictionary<string, string> colMapper = null)
        {
            SetTotalItemCount(q);
            bool isAscending = AspHelpers.IsSortDirAscending(sortDir);
            ViewBag.SortCol = sortCol;
            ViewBag.SortDir = sortDir;
            q = q.OrderByField(sortCol, enumType, isAscending);
            q = ApplyPagination(q, page, pageSize);
            return q;
        }

        protected IQueryable<T> ApplyBrowse<T>(IQueryable<T> q, string sortCol, IEnumerable<string> orderedValues, string sortDir, int? page, int? pageSize, IDictionary<string, string> colMapper = null)
        {
            SetTotalItemCount(q);
            bool isAscending = AspHelpers.IsSortDirAscending(sortDir);
            ViewBag.SortCol = sortCol;
            ViewBag.SortDir = sortDir;
            q = q.OrderByField(sortCol, orderedValues, isAscending);
            q = ApplyPagination(q, page, pageSize);
            return q;
        }

        private static readonly IDictionary<string, string> NoMappings = new Dictionary<string, string>().AsReadOnly();

        protected IQueryable<T> ApplySort<T>(IQueryable<T> q, string sortCol, string sortDir, IDictionary<string, string> colMapper = null)
        {
            var m = new Dictionary<string, string>(colMapper ?? NoMappings);
            if (!m.ContainsKey("CreatedAt"))
            {
                m["CreatedAt"] = "CreatedAtUtc";
            }
            sortCol = StringHelpers.TrimOrNull(sortCol);
            bool isAscending = AspHelpers.IsSortDirAscending(sortDir);
            ViewBag.SortCol = sortCol;
            ViewBag.SortDir = sortDir;
            if (sortCol != null)
            {
                sortCol = m.FindOrMissing(sortCol, sortCol);
                q = q.OrderByField(sortCol, isAscending);
            }
            return q;
        }

        protected IQueryable<T> ApplyPagination<T>(IQueryable<T> q, int? page = null, int? pageSize = null)
        {
            var rowsPerPageString = Request.Cookies["rowsPerPage"];
            int rowsPerPage;
            if (!int.TryParse(rowsPerPageString, out rowsPerPage))
            {
                rowsPerPage = 10;
            }
            var s = rowsPerPage;
            var p = page.GetValueOrDefault();
            if (p < 1) p = 1;
            ViewBag.PaginationSupported = true;
            ViewBag.PageNum = p;
            ViewBag.PageSize = s;
            return q.Skip((p - 1) * s).Take(s).ToList().AsQueryable();
        }
    }
}
