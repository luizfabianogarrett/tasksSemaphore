using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Tm = System.Timers;

namespace TasksSemaphore
{
    class Program
    {
        public static SemaphoreSlim concurrencySemaphoreMain = new SemaphoreSlim(1);
        public static HttpClient http = new HttpClient();
        public static Tm.Timer timer = new Tm.Timer();
        public static Tm.Timer timerPopulate = new Tm.Timer();
        public static bool populate = true;
        public static List<Store> messages = new List<Store>();

        static void Main(string[] args)
        {

            timer.Enabled = true;
            timer.Interval = 3000;
            timer.Elapsed += Timer_Elapsed;
            timer.Start();


            timerPopulate.Enabled = true;
            timerPopulate.Interval = 1000 * 60 * 60;
            timerPopulate.Elapsed += TimerPopulate_Elapsed;
            timerPopulate.Start();

            Console.ReadKey();  
        }

        private static void TimerPopulate_Elapsed(object sender, Tm.ElapsedEventArgs e)
        {
            if (!populate)
                populate = true;
        }

        private static void Timer_Elapsed(object sender, Tm.ElapsedEventArgs e)
        {
            Console.WriteLine("Timer {0}", DateTime.Now);
            Proccess();
        }

        private static void Proccess()
        {
            timer.Enabled = false;
            //Console.WriteLine("Não posso executar {0}", DateTime.Now);
            concurrencySemaphoreMain.Wait();
            //Console.WriteLine("Agora posso executar {0}", DateTime.Now);


            int maxConcurrency = 1;

            if (populate)
            {
                Console.WriteLine("Obtendo dados");
                populate = false;
                messages.Clear();
                for (int i = 1; i < 100; i++)
                {
                    messages.Add(
                        new Store
                        {
                            Id = i,
                            Marketplaces = new List<Marketplace> { new Marketplace(11) }
                            
                        });
                }
            }
            //else
                //Console.WriteLine("Não posso popular");

            using (SemaphoreSlim concurrencySemaphore = new SemaphoreSlim(maxConcurrency))
            {
                List<Task> tasks = new List<Task>();
                foreach (var msg in messages)
                {
                    concurrencySemaphore.Wait();

                    var t = Task.Factory.StartNew(() =>
                    {

                        try
                        {
                            Execute(msg);
                        }
                        finally
                        {
                            concurrencySemaphore.Release();
                        }
                    });

                    tasks.Add(t);
                }

                Task.WaitAll(tasks.ToArray());

              
            }

            concurrencySemaphoreMain.Release();
            timer.Enabled = true;
        }

        public static void Execute(Store s)
        {


            try
            {

                foreach (var marketplace in s.Marketplaces)
                {
                    foreach (var job in marketplace.SyncJobs)
                    {

                        if (DateTime.Now.Subtract(job.LastSync) > job.SyncEvery)
                        {
                            Console.WriteLine("Processando Loja {0} Market {1} LastSync {2}", s.Id, marketplace.Id, job.LastSync);

                            var d = http.GetAsync(string.Format("http://localhost:64360/api/values/{0}", s.Id)).Result.Content.ReadAsStringAsync().Result;
                            job.Result = d;

                            job.LastSync = DateTime.Now;
                        }
                        else
                        {
                            //Console.WriteLine("Não Processando {0} LastSyncOrder {1}", s.Id, s.LastSyncOrder);
                        }
                    }
                }


            }
            catch
            {
                Console.WriteLine("Erro");
            }


        }


    }

    public class Store
    {
        public int Id { get; set; }
        public List<Marketplace> Marketplaces { get; set; }

        public Store()
        {
            Marketplaces = new List<Marketplace>();
        }
    }


    public class Marketplace
    {
        public int Id { get; set; }
        public List<SyncJob> SyncJobs { get; set; }
               
        public Marketplace(int id)
        {
            SyncJobs = new List<SyncJob>();

            Id = id;

            Setup();
           
        }

        private void Setup()
        {
            if (Id == 11)
            {
                SyncJobs.Add(
                    new SyncJob
                    {
                        SyncType = SyncType.Orders,
                        LastSync = DateTime.Now,
                        SyncEvery = TimeSpan.FromMinutes(1),
                        Url = "http://localhost:64360/api/values/{0}"
                    });
            }
        }


        

    }

    public class SyncJob
    {
        public SyncType SyncType { get; set; }
        public DateTime LastSync { get; set; }
        public TimeSpan SyncEvery { get; set; }
        public string Url { get; set; }
        public string Result { get; set; }
    }


    public enum SyncType
    {
        Orders = 1,
        Products = 2,
    }


}
