﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using NLog;
using System.Data;
using Newtonsoft.Json;
using FusionPrePlaner.Algorithm;
namespace FusionPrePlaner
{
    class PrePlanner
    {

        public static Logger logger = LogManager.GetLogger("PrePlan");

        private static List<PrePlanner> _prePlannerList ;

        public static List<PrePlanner> PrePlannerList
        {
            get
            {
                if(_prePlannerList == null)
                {
                    _prePlannerList = new List<PrePlanner>();
                    foreach (var sto in ScrumTeamOwner.STO_FULL_LIST)
                    {

                        _prePlannerList.Add(new PrePlanner(sto));
                    }
                }
                
                return _prePlannerList;
            }
        }

        public static PrePlanner GetPrePlannerFromTeamCode(string code )
        {
            return  PrePlannerList.Where<PrePlanner>(planner => planner.Sto.Code == code).ToList()[0];
           
        }
            


        private ScrumTeamOwner Sto;
        private DataTable _sto_allIssues;
        public DataTable STO_AllIssues
        {
            get
            {
                if(_sto_allIssues == null)
                {
                    _sto_allIssues = new DataTable("Issues");
                    _sto_allIssues.Columns.Add(new DataColumn("Key", typeof(string)));
                    _sto_allIssues.Columns.Add(new DataColumn("Item ID", typeof(string)));
                    _sto_allIssues.Columns.Add(new DataColumn("FP", typeof(string)));                 
                    _sto_allIssues.Columns.Add(new DataColumn("Priority", typeof(string)));
                    _sto_allIssues.Columns.Add(new DataColumn("STO", typeof(string)));
                    _sto_allIssues.Columns.Add(new DataColumn("Entity REL", typeof(string)));
                    _sto_allIssues.Columns.Add(new DataColumn("Status", typeof(string)));

                    _sto_allIssues.Columns.Add(new DataColumn("Start FB", typeof(string)));
                    _sto_allIssues.Columns.Add(new DataColumn("End FB", typeof(string)));
                    _sto_allIssues.Columns.Add(new DataColumn("Target FB", typeof(string)));

                    _sto_allIssues.Columns.Add(new DataColumn("Ori Eff", typeof(string)));
                    _sto_allIssues.Columns.Add(new DataColumn("Rem Eff", typeof(string)));

                }
                return _sto_allIssues;
            }
        }


        private DataTable _dtAvai;
        private DataTable _dtUntouch;
        private DataTable DT_FB;
        private DataTable DT_REL;

        public void get_FB(DataTable DT_FB)
        {
            this.DT_FB = DT_FB;
        }

        public void get_REL(DataTable DT_REL)
        {
            this.DT_REL = DT_REL;
        }

        public DataTable DT_AvailIssues
        {
            get
            {
                if (_dtAvai == null)
                {
                    _dtAvai = STO_AllIssues.Clone();

                }
                return _dtAvai;
            }
        }
        public DataTable DT_UntouchableIssues
        {
            get
            {
                if (_dtUntouch == null)
                {
                    _dtUntouch = STO_AllIssues.Clone();

                }
                return _dtUntouch;
            }
        }

        public PrePlanner(ScrumTeamOwner sto)
        {
            Sto = sto;
        }
        private delegate void ProcessSTO_delegate();

      
        public void AsyncProcessSTO()
        {
            ProcessSTO_delegate dele = new ProcessSTO_delegate(ProcessSTO);
            var res = dele.BeginInvoke(null, null);        
        }

        
        public void ProcessSTO()
        {
            if(Sto.Selected == true && Sto.Run_Stat == STO_RUN_STAT.TO_RUN)
            {
                Sto.Run_Stat = STO_RUN_STAT.RUNNING;
                Program.fmMainWindow.RefreshUIDgvSTO();
                
                ExecuteAlgorithm();
                Sto.Run_Stat = STO_RUN_STAT.TO_RUN;
                Program.fmMainWindow.RefreshUIDgvSTO();
            }

        }
        private void ExecuteAlgorithm()
        {
           
            logger.Info("ExecuteAlgorithm for STO " + Sto.Name);

            Program.fmMainWindow.RefreshUIDgvAvailIssues();
            Program.fmMainWindow.RefreshUIDgvUntouchableIssues();
            
            
            
            GetAllIssues();

            PrePlan preplan = new PrePlan(DT_AvailIssues, DT_UntouchableIssues, new FeatureBuildChecker(), DT_FB,DT_REL);
            preplan.Process(Sto.Name);

            Program.fmMainWindow.RefreshUIDgvAvailIssues();
            Program.fmMainWindow.RefreshUIDgvUntouchableIssues();


        }

        public  void GetAllIssues()
        {
            //string strFilter = string.Format("search?jql=cf[29790]={0}%20and%20status=Open", Sto.Code) ;

            STO_AllIssues.Rows.Clear();
            DT_AvailIssues.Rows.Clear();
            DT_UntouchableIssues.Rows.Clear();
            string strFilter = string.Format("search?jql=cf[29790]={0}", Sto.Code);
            string strFields = "&fields=customfield_37381,customfield_38702,customfield_38719,customfield_29790,status,customfield_38751,customfield_38694,customfield_38693,timetracking,customfield_38725";
            string strOrderby = "+order+by+cf[38719]";
            string strSearch = strFilter + strOrderby + strFields;
            string url = System.IO.Path.Combine(Config.Instance.RestApi_Path, strSearch);
          
            int curIssueNum = 0;
            int totalIssueNum = 1;

            while (curIssueNum < totalIssueNum)
            {
                string cmd = "&startAt=" + curIssueNum.ToString();
                string json = RestAPIAccess.ExecuteRestAPI_CURL(Config.Instance.UserName, Config.Instance.Password, url, "GET", cmd);
                try
                {
                    RootObject rb = JsonConvert.DeserializeObject<RootObject>(json);
                    if (rb.issues == null)
                    {
                        logger.Error("Data error:" + json);
                        return;
                    }
                    foreach (Issues issue in rb.issues)
                    {

                        TableObject newTabObj = new TableObject(issue);
                        STO_AllIssues.Rows.Add(newTabObj.Key, newTabObj.ItemID, newTabObj.FP, newTabObj.UnifiedPriority, newTabObj.ScrumTeamOwner, newTabObj.LeadRelease,
                           newTabObj.Status, newTabObj.StartFB, newTabObj.EndFB, newTabObj.TargetFB, newTabObj.OriginalEffort, newTabObj.RemWorkEffort);
                        var dt = newTabObj.Status == "Open" ? DT_AvailIssues : DT_UntouchableIssues;
                        dt.Rows.Add(newTabObj.Key,newTabObj.ItemID, newTabObj.FP, newTabObj.UnifiedPriority, newTabObj.ScrumTeamOwner, newTabObj.LeadRelease,
                           newTabObj.Status, newTabObj.StartFB, newTabObj.EndFB, newTabObj.TargetFB, newTabObj.OriginalEffort, newTabObj.RemWorkEffort);
                    }
                    totalIssueNum = Convert.ToInt32(rb.total);
                    curIssueNum += rb.issues.Count;
                }
                catch(SystemException exp)
                {
                    logger.Error("exception:" + exp.Message +"---" + json);
                    return;
                }   
               
            }
            
        }
        

        

    }
}
