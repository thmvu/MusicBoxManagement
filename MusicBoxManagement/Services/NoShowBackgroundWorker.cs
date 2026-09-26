using System;
using System.Diagnostics;
using System.Threading;
using System.Web.Hosting;
using MusicBoxManagement.Models;

namespace MusicBoxManagement.Services
{
    public sealed class NoShowBackgroundWorker : IRegisteredObject
    {
        private readonly Timer timer;
        private int running;
        private int stopped;

        private NoShowBackgroundWorker()
        {
            timer = new Timer(Run, null, Timeout.Infinite, Timeout.Infinite);
        }

        public static NoShowBackgroundWorker Start()
        {
            var worker = new NoShowBackgroundWorker();
            HostingEnvironment.RegisterObject(worker);
            worker.timer.Change(TimeSpan.Zero, TimeSpan.FromMinutes(1));
            return worker;
        }

        private void Run(object state)
        {
            if (Interlocked.CompareExchange(ref running, 1, 0) != 0) return;
            try
            {
                if (Interlocked.CompareExchange(ref stopped, 0, 0) != 0) return;
                using (var db = new ApplicationDbContext())
                    new NoShowService(db, new SystemClock()).ProcessExpired();
            }
            catch (Exception error)
            {
                Trace.TraceError("NoShow worker failed: {0}", error);
            }
            finally
            {
                Interlocked.Exchange(ref running, 0);
            }
        }

        public void Stop(bool immediate)
        {
            if (Interlocked.Exchange(ref stopped, 1) != 0) return;
            timer.Dispose();
            HostingEnvironment.UnregisterObject(this);
        }
    }
}
