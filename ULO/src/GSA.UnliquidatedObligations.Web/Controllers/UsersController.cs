﻿using Autofac;
using GSA.UnliquidatedObligations.BusinessLayer.Authorization;
using GSA.UnliquidatedObligations.BusinessLayer.Data;
using GSA.UnliquidatedObligations.Web.Models;
using RevolutionaryStuff.Core;
using RevolutionaryStuff.Core.Caching;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.UI.WebControls.Expressions;

namespace GSA.UnliquidatedObligations.Web.Controllers
{
    [Authorize]
    [ApplicationPermissionAuthorize(ApplicationPermissionNames.ApplicationUser)]
    [ApplicationPermissionAuthorize(ApplicationPermissionNames.ManageUsers)]
    public class UsersController : BaseController
    {
        public const string Name = "Users";

        public static class ActionNames
        {
            public const string Index = "Index";
        }

        private readonly ApplicationUserManager UserManager;

        public UsersController(ApplicationUserManager userManager, ULODBEntities db, IComponentContext context, ICacher cacher)
            : base(db, context, cacher)
        {
            UserManager = userManager;
        }

        [Route("Users")]
        [ActionName(ActionNames.Index)]
        public async Task<ActionResult> Index(string username, string sortCol, string sortDir, int? page, int? pageSize)
        {
            username = StringHelpers.TrimOrNull(username);
            var users = ApplyBrowse(
                DB.AspNetUsers.Where(u => username == null || u.UserName.Contains(username)),
                sortCol ?? nameof(AspNetUser.UserName), sortDir, page, pageSize);
            var m = await CreateModelAsync(users);
            return View(m);
        }

        private async Task<IList<UserModel>> CreateModelAsync(IEnumerable<AspNetUser> aspNetUsers)
        {
            var users = aspNetUsers.ToList();
            var userIds = users.ConvertAll(u => u.Id);
            var groups = DB.UserUsers.Include(uu=>uu.ParentUser).Where(uu => userIds.Contains(uu.ChildUserId));
            var applicationPermissionClaims = await DB.AspnetUserApplicationPermissionClaims.Where(c => userIds.Contains(c.UserId)).ToListAsync();
            var subjectCategoryClaims = await DB.AspnetUserSubjectCategoryClaims.Where(c => userIds.Contains(c.UserId)).ToListAsync();
            var groupsByUserId = groups.ToMultipleValueDictionary(z => z.ChildUserId);
            var applicationPermissionClaimsByUserId = applicationPermissionClaims.ToMultipleValueDictionary(z => z.UserId);
            var subjectCategoryClaimsByUserId = subjectCategoryClaims.ToMultipleValueDictionary(z => z.UserId);
            var userModels = users.ConvertAll(u => new UserModel(u, groupsByUserId[u.Id], applicationPermissionClaimsByUserId[u.Id], subjectCategoryClaimsByUserId[u.Id]));
            return userModels;
        }

        private Task PopulateDetailsViewBag()
        {
            var userNames =
                from u in DB.AspNetUsers
                where u.UserType == AspNetUser.UserTypes.Group
                select u.UserName;
            ViewBag.GroupNames = userNames.Distinct().OrderBy().ToList();
            return Task.CompletedTask;
        }

        [Route("Users/{username}", Order =2)]
        public async Task<ActionResult> Details(string username)
        {
            var users = await DB.AspNetUsers.Where(u => u.UserName == username).ToListAsync();
            if (users.Count != 1) return HttpNotFound();
            var m = await CreateModelAsync(users);
            await PopulateDetailsViewBag();
            return View(m.Single());
        }

        [Route("Users/Create", Order =1)]
        public async Task<ActionResult> Create()
        {
            await PopulateDetailsViewBag();
            return View(new UserModel());
        }

        [Route("Users/Save")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Save(
            [Bind(Include =
                nameof(UserModel.UserId)+","+
                nameof(UserModel.UserName)+","+
                nameof(UserModel.Email)+","+
                nameof(UserModel.UserType)+","+
                nameof(UserModel.Claims)+","+
                nameof(UserModel.Permissions)+","+
                nameof(UserModel.Groups)+","+
                nameof(UserModel.SubjectCategoryClaims)+","+
                nameof(UserModel.SubjectCategoryClaims)+"."+nameof(SubjectCatagoryClaimValue.BACode)+","+
                nameof(UserModel.SubjectCategoryClaims)+"."+nameof(SubjectCatagoryClaimValue.DocType)+","+
                nameof(UserModel.SubjectCategoryClaims)+"."+nameof(SubjectCatagoryClaimValue.OrgCode)+","+
                nameof(UserModel.SubjectCategoryClaims)+"."+nameof(SubjectCatagoryClaimValue.Regions)
            )]
            UserModel m)
        {
            AspNetUser u = null;
            m.UserId = StringHelpers.TrimOrNull(m.UserId);
            m.UserName = StringHelpers.TrimOrNull(m.UserName);
            m.Email = StringHelpers.TrimOrNull(m.Email);
            var users = await DB.AspNetUsers.Where(z => z.UserName == m.UserName || z.Email == m.Email || z.Id == m.UserId).ToListAsync();
            bool hasErrors = false;
            foreach (var user in users)
            {
                if (user.Id == m.UserId)
                {
                    u = user;
                    continue;
                }
                if (0 == string.Compare(m.UserName, user.UserName, true))
                {
                    ModelState.AddModelError(nameof(m.UserName), $"Username {m.UserName} already exists, cannot re-add");
                    hasErrors = true;
                }
                if (0 == string.Compare(m.Email, user.Email, true))
                {
                    ModelState.AddModelError(nameof(m.Email), $"Email {m.Email} already exists, cannot re-add");
                    hasErrors = true;
                }
            }
            if (ModelState.IsValid && !hasErrors)
            {
                if (u == null && m.UserId == null)
                {
                    var res = await UserManager.CreateAsync(new ApplicationUser { UserName = m.UserName, Email = StringHelpers.TrimOrNull(m.Email) });
                    u = await DB.AspNetUsers.FirstOrDefaultAsync(z => z.UserName == m.UserName);
                }
                else if (u==null)
                {
                    return HttpNotFound();
                }
                else
                {
                    u.Email = m.Email;
                    u.UserName = m.UserName;
                }

                var myGroups = await DB.AspNetUsers.Where(g => g.UserType == AspNetUser.UserTypes.Group && m.Groups.Contains(g.UserName)).ToListAsync();
                foreach (var uu in u.ChildUserUsers.ToList())
                {
                    DB.UserUsers.Remove(uu);
                }
                myGroups.ForEach(g => DB.UserUsers.Add(new UserUser { ParentUser = g, ChildUser = u }));
                foreach (var c in u.AspNetUserClaims.Where(z => z.ClaimType == ApplicationPermissionClaimValue.ClaimType || z.ClaimType == SubjectCatagoryClaimValue.ClaimType).ToList())
                {
                    DB.AspNetUserClaims.Remove(c);
                }
                var allRegions = Cacher.FindOrCreateValWithSimpleKey("allRegionsHashSet", () => DB.Regions.Select(z => z.RegionId).ToSet());
                foreach (var p in m.Permissions)
                {
                    DB.AspNetUserClaims.Add(new AspNetUserClaim
                    {
                        ClaimType = ApplicationPermissionClaimValue.ClaimType,
                        UserId = u.Id,
                        AspNetUser = u,
                        ClaimValue = new ApplicationPermissionClaimValue
                        {
                            ApplicationPermissionName = Parse.ParseEnum<ApplicationPermissionNames>(p),
                            Regions = allRegions
                        }.ToXml()
                    });
                }
                var sccDocTypes = HttpContext.Request.Form.GetValues("scc.docType") ?? Empty.StringArray;
                var sccBaCodes = HttpContext.Request.Form.GetValues("scc.bacode") ?? Empty.StringArray;
                var sccOrgCodes = HttpContext.Request.Form.GetValues("scc.orgcode") ?? Empty.StringArray;
                var sccRegions = HttpContext.Request.Form.GetValues("scc.region") ?? Empty.StringArray;
                m.Claims.Clear();
                m.SubjectCategoryClaims = new List<AspnetUserSubjectCategoryClaim>();
                for (int z = 0; z < sccDocTypes.Length; ++z)
                {
                    var sccv = new SubjectCatagoryClaimValue
                    {
                        BACode = sccBaCodes[z],
                        DocType = sccDocTypes[z],
                        OrgCode = sccOrgCodes[z]
                    };
                    if (null == StringHelpers.TrimOrNull(sccv.DocType)) continue;
                    int regionId = Parse.ParseInt32(sccRegions[z]);
                    if (regionId > 0)
                    {
                        sccv.Regions = sccv.Regions ?? new HashSet<int>();
                        sccv.Regions.Add(regionId);
                    }
                    DB.AspNetUserClaims.Add(new AspNetUserClaim
                    {
                        ClaimType = SubjectCatagoryClaimValue.ClaimType,
                        UserId = u.Id,
                        AspNetUser = u,
                        ClaimValue = sccv.ToXml()
                    });
                    var scc = new AspnetUserSubjectCategoryClaim
                    {
                        BACode = sccv.BACode,
                        DocumentType = sccv.DocType,
                        OrgCode = sccv.OrgCode,
                    };
                    m.SubjectCategoryClaims.Add(scc);
                    m.Claims.Add(scc.ToFriendlyString());
                }
                await DB.SaveChangesAsync();
                return RedirectToIndex();
            }
            Mulligan:
            await PopulateDetailsViewBag();
            if (m.UserId == null) return View("Create", m);
            else return View("Details", m);
        }
    }
}
