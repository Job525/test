
using SyncService.Common;
using System;
using System.Collections.Generic;

namespace SyncService
{
    public class Program
    {
        static void Main(string[] args)
        {
            //List<KeyValuePair<string, string>> log = new List<KeyValuePair<string, string>>();
            //log.Add(new KeyValuePair<string, string>("1", "2"));
            //log.Add(new KeyValuePair<string, string>("1", "2"));
            //log.Add(new KeyValuePair<string, string>("1", "2"));
            //foreach (var item in log)
            //{
            //    Console.WriteLine(item.Key + item.Value);
            //}
            //Console.ReadKey();  rwer werwerew   是的撒大所多
            ContractDAL contractDAL = new ContractDAL();    
            contractDAL.UploadContractToFtp();
        }
    }
}
