using System.Collections.Concurrent;

namespace LiteInvest.Entity.PlazaEntity
{
    /// <summary>
    /// Used to control the rate of some occurrence per unit of time.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     To control the rate of an action using a <see cref="RateGate"/>, 
    ///     code should simply call <see cref="WaitToProceed()"/> prior to 
    ///     performing the action. <see cref="WaitToProceed()"/> will block
    ///     the current thread until the action is allowed based on the rate 
    ///     limit.
    ///     </para>
    ///     <para>
    ///     This class is thread safe. A single <see cref="RateGate"/> instance 
    ///     may be used to control the rate of an occurrence across multiple 
    ///     threads.
    ///     </para>
    /// </remarks>
    public class RateGate : IDisposable
    {
        // Semaphore used to count and limit the number of occurrences per
        // unit time.
        private readonly Semaphore _semaphore;

        // Times (in millisecond ticks) at which the semaphore should be exited.
        private readonly ConcurrentQueue<long> _exitTimes;

        // Timer used to trigger exiting the semaphore.
       // private readonly Timer _exitTimer;

        // Whether this instance is disposed.
        private bool _isDisposed;

        /// <summary>
        /// Number of occurrences allowed per unit of time.
        /// </summary>
        public int Occurrences { get; private set; }

        /// <summary>
        /// The length of the time unit, in milliseconds.
        /// </summary>
        public int TimeUnitMilliseconds { get; private set; }

        /// <summary>
        /// Initializes a <see cref="RateGate"/> with a rate of <paramref name="occurrences"/> 
        /// per <paramref name="timeUnit"/>.
        /// </summary>
        /// <param name="occurrences">Number of occurrences allowed per unit of time.</param>
        /// <param name="timeUnit">Length of the time unit.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// If <paramref name="occurrences"/> or <paramref name="timeUnit"/> is negative.
        /// </exception>
        public RateGate(int occurrences, TimeSpan timeUnit)
        {
            // Check the arguments.
            if (occurrences <= 0)
                throw new ArgumentOutOfRangeException("occurrences", "Number of occurrences must be a positive integer");
            if (timeUnit != timeUnit.Duration())
                throw new ArgumentOutOfRangeException("timeUnit", "Time unit must be a positive span of time");
            if (timeUnit >= TimeSpan.FromMilliseconds(UInt32.MaxValue))
                throw new ArgumentOutOfRangeException("timeUnit", "Time unit must be less than 2^32 milliseconds");

            Occurrences = occurrences;
            TimeUnitMilliseconds = (int)Math.Floor(timeUnit.TotalMilliseconds * 1.1);  // % засапа

            // Create the semaphore, with the number of occurrences as the maximum count.
            _semaphore = new Semaphore(Occurrences, Occurrences);

            // Create a queue to hold the semaphore exit times.
            _exitTimes = new ConcurrentQueue<long>();

            threadTimeController = new Thread(TimeSemaphoreController);
            threadTimeController.IsBackground = true;
            threadTimeController.Name = "TimeController";
            threadTimeController.Start();
        }

        Thread threadTimeController;

        private void TimeSemaphoreController()
        {
            int countSemafor = 0;
            while (true)
            {
                try
                {
                    Thread.Sleep(10);
                    while (_exitTimes.TryPeek(out long exitTime)
                            && exitTime <= DateTime.Now.Ticks)
                    {
                        countSemafor  = _semaphore.Release();
                        _exitTimes.TryDequeue(out _);
                    }
                    if (_exitTimes.Count == 0 && countSemafor >= Occurrences)
                    {
                        countSemafor  = _semaphore.Release();
                    }
                }
                catch (ThreadAbortException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    
                }
            }

        }


       
        /// <summary>
        /// Blocks the current thread until allowed to proceed or until the
        /// specified timeout elapses.
        /// </summary>
        /// <param name="millisecondsTimeout">Number of milliseconds to wait, or -1 to wait indefinitely.</param>
        /// <returns>true if the thread is allowed to proceed, or false if timed out</returns>
        public bool WaitToProceed(int millisecondsTimeout)
        {
            // Check the arguments.
            if (millisecondsTimeout < -1)
                throw new ArgumentOutOfRangeException("millisecondsTimeout");

            CheckDisposed();

            // Block until we can enter the semaphore or until the timeout expires.
            var entered = _semaphore.WaitOne(millisecondsTimeout);

            
            if (entered)
            {
                long timeToExit = DateTime.Now.Ticks + TimeUnitMilliseconds * TimeSpan.TicksPerMillisecond;
                _exitTimes.Enqueue(timeToExit);
            }

            return entered;
        }

        /// <summary>
        /// Blocks the current thread until allowed to proceed or until the
        /// specified timeout elapses.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns>true if the thread is allowed to proceed, or false if timed out</returns>
        public bool WaitToProceed(TimeSpan timeout)
        {
            return WaitToProceed((int)timeout.TotalMilliseconds);
        }

        /// <summary>
        /// Blocks the current thread indefinitely until allowed to proceed.
        /// </summary>
        public void WaitToProceed()
        {
            WaitToProceed(Timeout.Infinite);
        }

        // Throws an ObjectDisposedException if this object is disposed.
        private void CheckDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException("RateGate is already disposed");
        }

        /// <summary>
        /// Releases unmanaged resources held by an instance of this class.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged resources held by an instance of this class.
        /// </summary>
        /// <param name="isDisposing">Whether this object is being disposed.</param>
        protected virtual void Dispose(bool isDisposing)
        {
            if (!_isDisposed)
            {
                if (isDisposing)
                {
                    // The semaphore and timer both implement IDisposable and 
                    // therefore must be disposed.
                    _semaphore.Dispose();
                    //_exitTimer.Dispose();
                    threadTimeController.Abort();

                    _isDisposed = true;
                }
            }
        }
    }
}
