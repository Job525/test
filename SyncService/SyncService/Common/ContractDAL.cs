using Cn.Vcredit.Common.FileStorage;
using Cn.Vcredit.Common.Log;
using Cn.Vcredit.Data;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;

namespace SyncService.Common
{
    public class ContractDAL : BaseDao
    {

        /// <summary>
        /// 记录日志，实例化对象
        /// </summary>
        //private readonly new ILogger //_logger = LogFactory.CreateLogger(typeof(ContractDAL));
        private const string ProductKey = "LOANKIND/KAKADAI";
        public Dictionary<string, List<string>> orderids = new Dictionary<string, List<string>>();


        /// <summary>
        /// 上传合同到FTP
        /// </summary>
        public void UploadContractToFtp()
        {
            Console.WriteLine(String.Format("--------------开始同步上传数据--------------"));
            try
            {
                FileStream fs;
                StreamWriter sw;
                var filePath = Common.FilePath;
                var filelocalsavepath = Common.LocalFileSavePath;
                var logpath = Path.Combine(filelocalsavepath, "Log");
                if (!Directory.Exists(logpath))
                {
                    Directory.CreateDirectory(logpath);
                }
                logpath = Path.Combine(logpath, DateTime.Now.ToString("yyyy-MM-dd") + ".log");
                if (File.Exists(logpath))
                {
                    fs = new FileStream(logpath, FileMode.Append, FileAccess.Write);
                }
                else
                {
                    fs = new FileStream(logpath, FileMode.Create, FileAccess.Write);
                }
                sw = new StreamWriter(fs);
                var number = Common.UploadNumber;
                int uploadNumber = 0;
                int.TryParse(number, out uploadNumber);
                DirectoryInfo dir = new DirectoryInfo(filePath);
                var lists = GetOrderIdByFilePath(dir, uploadNumber);
                foreach (var item in lists)
                {
                    var key = item.Key;
                    var value = item.Value;
                    //判断查询到合同信息
                    var order = GetOrderInfoByOrderId(key);
                    if (value.Count > 0)
                    {
                        foreach (var filename in value)
                        {
                            var filetempletpath = Path.Combine(filePath, filename + "\\" + key + ".pdf");
                            var localsavepath = Path.Combine(filelocalsavepath, filename);
                            var strLog = "   OrderId: " + key + "," + "  文件名称: " + filename;
                            //当前合同信息存在并且数据库中没有生成合同
                            if (order != null)
                            {
                                var filefullkey = GetFileFullKey(filename, order.LoanSideName);
                                if (!IsExistContract(order.Bid, filefullkey))
                                {
                                    CreateFileForPDF(order.Bid, filefullkey, filename, filetempletpath, key);
                                }
                                strLog = "   Bid: " + order.Bid + "," + strLog;
                            }
                            Console.WriteLine(strLog);
                            //保存当前文档到本地备份路径
                            SaveFileToLocalPath(filetempletpath, localsavepath, key);
                            sw.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "   ---   " + strLog);
                        }
                    }
                }
                sw.Close();
                fs.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("---------------上传程序报错 原因:" + ex.ToString()) + "-------------------------");
                Console.ReadKey();
            }
            Console.WriteLine(String.Format("--------------同步上传数据完成--------------"));
        }


        /// <summary>
        /// 将文件上传到FTP
        /// </summary>
        /// <param name="bid"></param>
        /// <param name="fileFullKey"></param>
        /// <param name="filename"></param>
        /// <param name="filetempletpath"></param>
        /// <param name="filelocalsavepath"></param>
        /// <param name="orderid"></param>
        public void CreateFileForPDF(string bid, string fileFullKey, string filename, string filetempletpath, string orderid)
        {
            byte[] readbyte = System.IO.File.ReadAllBytes(filetempletpath);
            int filesize = readbyte.Length;
            string fileName = Common.GetTempPdfFileName();
            var savepath = Common.FileSavePath;
            int Intbid = 0;
            int.TryParse(bid, out Intbid);
            if (filesize > 0 && Intbid > 0)
            {
                string sourcekeyrule = ProductKey.Replace("/", "");
                string typerule = fileFullKey.Replace("/", "");
                string FileSavePath = Path.Combine(savepath, sourcekeyrule, typerule).Replace("\\", "/");
                string filefullpath = Path.Combine(FileSavePath, fileName).Replace("\\", "/");
                //上传当前文档到FTP
                FtpTools.Upload(FileSavePath, filefullpath, readbyte);
                var busid = AddFileByproc(Intbid, fileName, filefullpath, filesize, EnumValue.AccountAdmin, filename, fileFullKey);
                if (busid > 0)
                {
                    AddContractFile(Intbid, busid);
                }
            }
        }

        /// <summary>
        /// 保存当前文档到本地备份路径
        /// </summary>
        /// <param name="filetempletpath"></param>
        /// <param name="filelocalsavepath"></param>
        /// <param name="orderid"></param>
        public void SaveFileToLocalPath(string filetempletpath, string filelocalsavepath, string orderid)
        {
            if (!Directory.Exists(filelocalsavepath))
            {
                Directory.CreateDirectory(filelocalsavepath);
            }
            filelocalsavepath = Path.Combine(filelocalsavepath, orderid + ".pdf");
            if (File.Exists(filetempletpath))
            {
                File.Copy(filetempletpath, filelocalsavepath, true);
                File.Delete(filetempletpath);
            }
        }

        /// <summary>
        /// 根据orderid获取Order信息
        /// </summary>
        /// <param name="orderid"></param>
        /// <returns></returns>
        public OrderInfo GetOrderInfoByOrderId(string orderid)
        {
            #region

            //string queryStr = @"SELECT  sign.Bid,buss.OrderId,sign.ContractNo 
            //                    FROM business.Business buss
            //                    JOIN sign.Sign  sign ON buss.Id=sign.Bid
            //                    JOIN apply.ApplyInfo appInfo  ON appInfo.Bid=buss.Id
            //                    WHERE buss.OrderId=@OrderId and 
            //                    appInfo.LoanKind='LOANKIND/KAKADAI'";

            #endregion

            string queryStr = @"SELECT buss.Id AS Bid,buss.OrderId,
                                LoanSideName=CASE lend.LoanSide WHEN 13 THEN ('中国对外经济贸易信托有限公司')--成都维仕小额贷款有限公司 更改放款方
                                ELSE detail.NAME END
                                FROM business.Business buss
                                JOIN apply.ApplyInfo appInfo ON appInfo.Bid=buss.Id
                                JOIN lending.Lending lend ON buss.Id=lend.bid
                                LEFT JOIN sys.Contract.ContractCodeDetail detail ON lend.LoanSide=detail.bankaccountid
                                WHERE buss.OrderId=@OrderId and  
                                appInfo.LoanKind='LOANKIND/KAKADAI' ";

            IDictionary<string, object> para = new Dictionary<string, object>();
            para.Add("@OrderId", orderid);
            var resultList = Query<OrderInfo>(queryStr, para, "LoanDB", CommandType.Text);
            if (resultList == null || resultList.Count == 0) return default(OrderInfo);
            return resultList[0];
        }

        /// <summary>
        /// 判断当前有没有合同
        /// </summary>
        /// <param name="bid"></param>
        /// <returns></returns>
        public bool IsExistContract(string bid, string fullkey)
        {
            string sqlstr = @"SELECT  count(*)
                              FROM business.BussDocument a
                              INNER JOIN sys.common.UserFile b ON a.FileId = b.Id
                              WHERE a.Bid=@Bid and a.Kind=@Kind";
            IDictionary<string, object> para = new Dictionary<string, object>();
            para.Add("@Bid", bid);
            para.Add("@Kind", fullkey);
            var resultList = Query<int>(sqlstr, para, "LoanDB", CommandType.Text);
            return resultList[0] > 0;
        }


        /// <summary>
        /// 添加记录到common.UserFile和business.BussDocument
        /// </summary>
        /// <param name="bid"></param>
        /// <param name="filename"></param>
        /// <param name="filepath"></param>
        /// <param name="filesize"></param>
        /// <param name="userid"></param>
        /// <param name="bussname"></param>
        /// <param name="BussKind"></param>
        /// <returns></returns>
        public int AddFileByproc(int bid, string filename, string filepath, int filesize, int userid, string bussname, string BussKind)
        {
            Dictionary<string, object> param = new Dictionary<string, object>();
            param.Add("@Bid", bid);
            param.Add("@AccountId", userid);
            param.Add("@FileName", filename);
            param.Add("@FilePath", filepath);
            param.Add("@BussKind", BussKind);
            param.Add("@BussName", bussname);
            param.Add("@FileSize", filesize);
            var resultList = Query<int>("business.proc_File_AddNewFile", param, "LoanDB", CommandType.StoredProcedure);
            if (resultList == null || resultList.Count == 0) return 0;
            return resultList[0];
        }

        /// <summary>
        /// 添加记录到[contract].[ContractFile]
        /// </summary>
        /// <param name="bid"></param>
        /// <param name="busid"></param>
        /// <returns></returns>
        public int AddContractFile(int bid, int busid)
        {
            string sql = @"
                    BEGIN TRAN  
                    INSERT INTO [contract].[ContractFile]
                    ( ContractNo ,BussDocId ,ContractStatus ,IsDeleted ,CreateTime ,EstampType)
                    SELECT  c.ContractNo ,-- 合同号
                    b.Id ,-- 业务文件Id
                    18 ,-- 待盖章状态
                    0 ,-- 未作废的
                    GETDATE() ,-- 创建时间
                    NULL-- 盖章不需要这个字段的值
                    FROM    [contract].[Contract] c
                    JOIN business.BussDocument b ON c.Bid = b.Bid
                    AND c.Bid = {0} AND b.Id = {1} AND NOT EXISTS 
                    ( SELECT 1 FROM [contract].[ContractFile] f WHERE f.[BussDocId] = b.Id ) 

                    UPDATE  business.BussDocument
                    SET     CustomerId = -888
                    WHERE   bid = {0} 
                    COMMIT TRAN   
                   ";
            sql = string.Format(sql, bid, busid);
            var ret = Execute(sql, null, "LoanDB");
            return ret > 0 ? 1 : 0;
        }

        /// <summary>
        /// 获取fileFullKey
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public string GetFileFullKey(string filename, string loanSideName)
        {
            #region 合同套件

            //GenerateContract(strCurrStoreKey, ProductKey, bid, "DOCUMENTKIND/KAKADAI/CD/DKHT", "个人信用贷款合同", signdate, dic, contractEntity.ContractNo);
            //GenerateContract(strCurrStoreKey, ProductKey, bid, "DOCUMENTKIND/KAKADAI/CD/FFHT", "个人借款服务与咨询合同", signdate, dic, contractEntity.ContractNo);
            //GenerateContract(strCurrStoreKey, ProductKey, bid, "DOCUMENTKIND/KAKADAI/CD/GRKHKKSQS", "个人客户扣款授权书", signdate, dic, contractEntity.ContractNo);
            //GenerateContract(strCurrStoreKey, ProductKey, bid, "DOCUMENTKIND/KAKADAI/CD/GRXFXTDKSPB", "个人消费信托贷款审批表", signdate, dic, contractEntity.ContractNo);
            //GenerateContract(strCurrStoreKey, ProductKey, bid, "DOCUMENTKIND/KAKADAI/CD/GRXXSQS", "个人信息授权书", signdate, dic, contractEntity.ContractNo);
            //GenerateContract(strCurrStoreKey, ProductKey, bid, "DOCUMENTKIND/KAKADAI/CD/GRXXJKHTBCXY", "个人信用借款合同补充协议", signdate, dic, contractEntity.ContractNo);
            //GenerateContract(strCurrStoreKey, ProductKey, bid, "DOCUMENTKIND/KAKADAI/CD/KKDFWXY", "卡卡贷服务协议", signdate, dic, contractEntity.ContractNo);
            //GenerateContract(strCurrStoreKey, ProductKey, bid, "DOCUMENTKIND/KAKADAI/CD/KKDKHDKSQB", "卡卡贷客户贷款申请表", signdate, dic, contractEntity.ContractNo);
            //GenerateContract(strCurrStoreKey, ProductKey, bid, "DOCUMENTKIND/KAKADAI/CD/TBTSH", "特别提示函", signdate, dic, contractEntity.ContractNo);


            //GenerateContract(strCurrStoreKey, ProductKey, bid, "DOCUMENTKIND/KAKADAI/JA/DKHT", "个人信用贷款合同", signdate, dic, contractEntity.ContractNo);
            //GenerateContract(strCurrStoreKey, ProductKey, bid, "DOCUMENTKIND/KAKADAI/JA/FFHT", "个人借款服务与咨询合同", signdate, dic, contractEntity.ContractNo);
            //GenerateContract(strCurrStoreKey, ProductKey, bid, "DOCUMENTKIND/KAKADAI/JA/GRKHKKSQS", "个人客户扣款授权书", signdate, dic, contractEntity.ContractNo);
            //GenerateContract(strCurrStoreKey, ProductKey, bid, "DOCUMENTKIND/KAKADAI/JA/GRXFXTDKSPB", "个人消费信托贷款审批表", signdate, dic, contractEntity.ContractNo);
            //GenerateContract(strCurrStoreKey, ProductKey, bid, "DOCUMENTKIND/KAKADAI/JA/GRXXSQS", "个人信息授权书", signdate, dic, contractEntity.ContractNo);
            //GenerateContract(strCurrStoreKey, ProductKey, bid, "DOCUMENTKIND/KAKADAI/JA/GRXXJKHTBCXY", "个人信用借款合同补充协议", signdate, dic, contractEntity.ContractNo);
            //GenerateContract(strCurrStoreKey, ProductKey, bid, "DOCUMENTKIND/KAKADAI/JA/KKDFWXY", "卡卡贷服务协议", signdate, dic, contractEntity.ContractNo);
            //GenerateContract(strCurrStoreKey, ProductKey, bid, "DOCUMENTKIND/KAKADAI/JA/KKDKHDKSQB", "卡卡贷客户贷款申请表", signdate, dic, contractEntity.ContractNo);
            //GenerateContract(strCurrStoreKey, ProductKey, bid, "DOCUMENTKIND/KAKADAI/JA/TBTSH", "特别提示函", signdate, dic, contractEntity.ContractNo);

            //GenerateContract(StoreKey, ProductKey, bid, "DOCUMENTKIND/KAKADAI/WM/DKHT", "贷款合同", signdate, dic, contractEntity.ContractNo);
            //GenerateContract(StoreKey, ProductKey, bid, "DOCUMENTKIND/KAKADAI/WM/FWHT", "个人贷款服务合同", signdate, dic, contractEntity.ContractNo);
            //GenerateContract(StoreKey, ProductKey, bid, "DOCUMENTKIND/KAKADAI/WM/DBHT", "个人贷款委托担保合同", signdate, dic, contractEntity.ContractNo);
            //GenerateContract(StoreKey, ProductKey, bid, "DOCUMENTKIND/KAKADAI/WM/GRKHKKSQS", "个人客户扣款授权书", signdate, dic, contractEntity.ContractNo);
            //GenerateContract(StoreKey, ProductKey, bid, "DOCUMENTKIND/KAKADAI/WM/GRXXKKSQS", "个人信息授权书", signdate, dic, contractEntity.ContractNo);
            //GenerateContract(StoreKey, ProductKey, bid, "DOCUMENTKIND/KAKADAI/WM/JKJJ", "借款借据", signdate, dic, contractEntity.ContractNo);
            //GenerateContract(StoreKey, ProductKey, bid, "DOCUMENTKIND/KAKADAI/WM/TBTSH", "特别提示函", signdate, dic, contractEntity.ContractNo);
            //GenerateContract(StoreKey, ProductKey, bid, "DOCUMENTKIND/KAKADAI/WM/KKDFWXY", "卡卡贷服务协议", signdate, dic, contractEntity.ContractNo);
            //GenerateContract(StoreKey, ProductKey, bid, "DOCUMENTKIND/KAKADAI/WM/KKDKHDKSQB", "卡卡贷客户贷款申请表", signdate, dic, contractEntity.ContractNo);

            //GenerateContract2(Convert.ToInt32(partid), bid, "DOCUMENTKIND/KAKADAI/FWHT", "个人贷款服务合同", dic, contractEntity.ContractNo);
            //GenerateContract2(Convert.ToInt32(partid), bid, "DOCUMENTKIND/KAKADAI/DBHT", "个人贷款委托担保合同", dic,                 contractEntity.ContractNo);
            //GenerateContract2(Convert.ToInt32(partid), bid, "DOCUMENTKIND/KAKADAI/KKSQS", "个人客户扣款授权书", dic,                 contractEntity.ContractNo);
            //GenerateContract2(Convert.ToInt32(partid), bid, "DOCUMENTKIND/KAKADAI/XXSQS", "个人信息授权书", dic,                 contractEntity.ContractNo);
            //GenerateContract2(Convert.ToInt32(partid), bid, "DOCUMENTKIND/KAKADAI/XYBGCXSQS", "个人信用报告查询授权书", dic,                 contractEntity.ContractNo);
            //GenerateContract2(Convert.ToInt32(partid), bid, "DOCUMENTKIND/KAKADAI/JKJJ", "借款借据", dic,                 contractEntity.ContractNo);
            //GenerateContract2(Convert.ToInt32(partid), bid, "DOCUMENTKIND/KAKADAI/DKSQB", "晋城银行个人无抵押消费贷款申请表", dic,                 contractEntity.ContractNo);
            //GenerateContract2(Convert.ToInt32(partid), bid, "DOCUMENTKIND/KAKADAI/FWXY", "卡卡贷服务协议", dic,                 contractEntity.ContractNo);
            //GenerateContract2(Convert.ToInt32(partid), bid, "DOCUMENTKIND/KAKADAI/DKHZXY", "卡卡贷个人无抵押消费贷款合作协议", dic,                 contractEntity.ContractNo);
            //GenerateContract2(Convert.ToInt32(partid), bid, "DOCUMENTKIND/KAKADAI/KHDKSQB", "卡卡贷客户贷款申请表", dic,                 contractEntity.ContractNo);
            //GenerateContract2(Convert.ToInt32(partid), bid, "DOCUMENTKIND/KAKADAI/TBTSH", "特别提示函", dic,                 contractEntity.ContractNo);

            //GenerateContract2(Convert.ToInt32(partid), bid, "DOCUMENTKIND/KAKADAI/HF/KYJKHT", "开源借款合同", dic,                   contractEntity.ContractNo);
            //GenerateContract2(Convert.ToInt32(partid), bid, "DOCUMENTKIND/KAKADAI/HF/WTDKJKHTJKJJ", "委托贷款借款合同借款借据", dic,                 contractEntity.ContractNo);
            //GenerateContract2(Convert.ToInt32(partid), bid, "DOCUMENTKIND/KAKADAI/HF/GRDKFWHT", "个人贷款服务合同", dic,                 contractEntity.ContractNo);
            //GenerateContract2(Convert.ToInt32(partid), bid, "DOCUMENTKIND/KAKADAI/HF/GRKHKKSQS", "个人客户扣款授权书", dic,                 contractEntity.ContractNo);
            //GenerateContract2(Convert.ToInt32(partid), bid, "DOCUMENTKIND/KAKADAI/HF/GRXXSQS", "个人信息授权书", dic,                 contractEntity.ContractNo);
            //GenerateContract2(Convert.ToInt32(partid), bid, "DOCUMENTKIND/KAKADAI/HF/TBTSH", "特别提示函", dic,                 contractEntity.ContractNo);


            //GenerateContract2(Convert.ToInt32(partid), bid, "DOCUMENTKIND/KAKADAI/HR/DKHT", "贷款合同", dic, contractEntity.ContractNo);
            //GenerateContract2(Convert.ToInt32(partid), bid, "DOCUMENTKIND/KAKADAI/HR/DKYTSM", "贷款用途声明", dic, contractEntity.ContractNo);
            //GenerateContract2(Convert.ToInt32(partid), bid, "DOCUMENTKIND/KAKADAI/HR/GRDKFWHT", "个人贷款服务合同", dic, contractEntity.ContractNo);
            //GenerateContract2(Convert.ToInt32(partid), bid, "DOCUMENTKIND/KAKADAI/HR/GRDKWTDBHT", "个人贷款委托担保合同", dic, contractEntity.ContractNo);
            //GenerateContract2(Convert.ToInt32(partid), bid, "DOCUMENTKIND/KAKADAI/HR/GRKHKKSQS", "个人客户扣款授权书", dic, contractEntity.ContractNo);
            //GenerateContract2(Convert.ToInt32(partid), bid, "DOCUMENTKIND/KAKADAI/HR/GRXXSQS", "个人信息授权书", dic, contractEntity.ContractNo);
            //GenerateContract2(Convert.ToInt32(partid), bid, "DOCUMENTKIND/KAKADAI/HR/JKJJ", "借款借据", dic, contractEntity.ContractNo);
            //GenerateContract2(Convert.ToInt32(partid), bid, "DOCUMENTKIND/KAKADAI/HR/TBTSH", "特别提示函", dic, contractEntity.ContractNo);
            //GenerateContract2(Convert.ToInt32(partid), bid, "DOCUMENTKIND/KAKADAI/HR/KKDKHDKSQB", "卡卡贷客户贷款申请表", dic, contractEntity.ContractNo);
            //GenerateContract2(Convert.ToInt32(partid), bid, "DOCUMENTKIND/KAKADAI/HR/KKDFWXY", "卡卡贷服务协议", dic, contractEntity.ContractNo);

            #endregion

            //if (filename == "个人消费信托贷款及服务合同"||)
            //{
            //    return "DOCUMENTKIND/HETONG_Blank";
            //}
            //else if (filename == "个人消费信托贷款借款借据")
            //{
            //    return "DOCUMENTKIND/JIEKUANJIEJU_Blank";
            //}
            //else
            //{
            #region 拼接路径

            var fileFullKey = string.Empty;
            var loanside = string.Empty;
            switch (filename)
            {
                case "个人消费信托贷款及服务合同":
                    fileFullKey = "JKJJ";
                    break;
                case "个人消费信托贷款借款借据":
                    fileFullKey = "JKJJ";
                    break;
                case "个人借款服务与咨询合同":
                    fileFullKey = "FFHT";
                    break;
                case "个人客户扣款授权书":
                    if (loanSideName == "晋城银行")
                    {
                        fileFullKey = "KKSQS";
                    }
                    else
                    {
                        fileFullKey = "GRKHKKSQS";
                    }
                    break;
                case "个人消费信托贷款审批表":
                    fileFullKey = "GRXFXTDKSPB";
                    break;
                case "个人信息授权书":
                    if (loanSideName == "中国对外经济贸易信托有限公司")
                    {
                        fileFullKey = "GRXXKKSQS";
                    }
                    else if (loanSideName == "晋城银行")
                    {
                        fileFullKey = "XXSQS";
                    }
                    else
                    {
                        fileFullKey = "GRXXSQS";
                    }
                    break;
                case "个人信用贷款合同":
                    fileFullKey = "DKHT";
                    break;
                case "个人信用借款合同补充协议":
                    fileFullKey = "GRXXJKHTBCXY";
                    break;
                case "关于变更卡号的补充协议":
                    fileFullKey = "BGKHBCXY";
                    break;
                case "卡卡贷平台服务合同":
                    fileFullKey = "KKDPTFWHT";
                    break;
                case "卡卡贷服务协议":
                    if (loanSideName == "晋城银行")
                    {
                        fileFullKey = "FWXY";
                    }
                    else
                    {
                        fileFullKey = "KKDFWXY";
                    }
                    break;
                case "卡卡贷客户贷款申请表":
                    if (loanSideName == "晋城银行")
                    {
                        fileFullKey = "KHDKSQB";
                    }
                    else
                    {
                        fileFullKey = "KKDKHDKSQB";
                    }
                    break;
                case "特别提示函":
                    fileFullKey = "TBTSH";
                    break;
                case "贷款用途声明":
                    fileFullKey = "DKYTSM";
                    break;
                default:
                    break;
            }
            switch (loanSideName)
            {
                case "成都维仕小额贷款有限公司":
                    loanside = "CD/";
                    break;
                case "上海静安维信小额贷款有限公司":
                    loanside = "JA/";
                    break;
                case "中国对外经济贸易信托有限公司":
                    loanside = "WM/";
                    break;
                case "晋城银行":
                    loanside = "";
                    break;
                case "恒丰银行":
                    loanside = "HF/";
                    break;
                case "对外经济贸易信托-华瑞银行":
                    loanside = "HR/";
                    break;
                default:
                    loanside = "";
                    break;
            }
            return "DOCUMENTKIND/KAKADAI/" + loanside + fileFullKey;

            #endregion
            // }
        }

        /// <summary>
        /// 获取文件名称和文件夹名称
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        public Dictionary<string, List<string>> GetOrderIdByFilePath(FileSystemInfo info, int uploadNumber)
        {
            //_logger.Error(String.Format("GetOrderIdByFilePath:开始获取文件名称和文件夹名称"));
            DirectoryInfo folder = info as DirectoryInfo;
            if (!folder.Exists)
            {
                return orderids;
            }
            FileSystemInfo[] files = folder.GetFileSystemInfos();
            for (int i = 0; i < files.Length; i++)
            {
                if (orderids.Count < uploadNumber)
                {
                    FileInfo file = files[i] as FileInfo;
                    if (file != null)
                    {
                        var key = file.Name.Remove(file.Name.LastIndexOf("."));
                        var filename = info.Name;
                        //如果列表不存在当前的orderid,则添加orderid和当前文件夹名称
                        if (!orderids.ContainsKey(key))
                        {
                            orderids.Add(key, new List<string> { filename });
                        }
                        else
                        {
                            //如果存在当前的orderid,则判断当前的orderid是否已经有当前文件夹名称,如果不存在则添加
                            var filenames = orderids[key];
                            if (!filenames.Contains(filename))
                            {
                                filenames.Add(filename);
                                orderids[key] = filenames;
                            }
                        }
                    }
                    //对于子目录，进行递归调用 
                    else
                    {
                        GetOrderIdByFilePath(files[i], uploadNumber);
                    }
                }
                else
                {
                    return orderids;
                }
            }
            //_logger.Error(String.Format("GetOrderIdByFilePath:获取文件名称和文件夹名称结束"));
            return orderids;
        }

        public class Common
        {
            /// <summary>
            /// 获取临时文件名
            /// </summary>
            /// <returns></returns>
            public static string GetTempPdfFileName()
            {
                return DateTime.Now.ToString("yyyyMMddhhmmss").ToString() + Guid.NewGuid().ToString() + ".pdf";
            }

            private static string _FileSavePath;
            public static string FileSavePath
            {
                get
                {
                    if (string.IsNullOrEmpty(_FileSavePath))
                    {
                        _FileSavePath = ConfigurationManager.AppSettings["FileSavePath"].ToString();
                    }
                    return _FileSavePath;
                }
            }

            private static string _LocalFileSavePath;
            public static string LocalFileSavePath
            {
                get
                {
                    if (string.IsNullOrEmpty(_LocalFileSavePath))
                    {
                        _LocalFileSavePath = ConfigurationManager.AppSettings["LocalFileSavePath"].ToString();
                    }
                    return _LocalFileSavePath;
                }
            }

            private static string _FilePath;
            public static string FilePath
            {
                get
                {
                    if (string.IsNullOrEmpty(_FilePath))
                    {
                        _FilePath = ConfigurationManager.AppSettings["FilePath"].ToString();
                    }
                    return _FilePath;
                }
            }

            private static string _UploadNumber;
            public static string UploadNumber
            {
                get
                {
                    if (string.IsNullOrEmpty(_UploadNumber))
                    {
                        _UploadNumber = ConfigurationManager.AppSettings["UploadNumber"].ToString();
                    }
                    return _UploadNumber;
                }
            }
        }

        public class EnumValue
        {
            public static int AccountAdmin = 1;
        }

        public class OrderInfo
        {
            public string Bid { get; set; }
            public string OrderId { get; set; }
            /// <summary>
            /// 放款方
            /// </summary>
            public string LoanSideName { get; set; }
        }
    }

}
