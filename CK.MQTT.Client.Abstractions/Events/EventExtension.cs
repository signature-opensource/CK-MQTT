using CK.Core;
using System;
using System.Threading.Tasks;

namespace CK.MQTT
{
    public static class EventExtension
    {

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TSender"></typeparam>
        /// <typeparam name="TArg"></typeparam>
        /// <param name="eventHandler"></param>
        /// <param name="predicate"></param>
        /// <param name="timeoutMillisecond">When -1, waits indefinitely, When 0, the message must be already be available.</param>
        /// <returns></returns>
        public static Task<TArg?> WaitAsync<TSender, TArg>( this SequentialEventHandlerSender<TSender, TArg> eventHandler, Func<TArg, bool>? predicate = null, int timeoutMillisecond = -1 )
          where TArg : class
        {
            TaskCompletionSource<TArg?> taskCompletionSource = new TaskCompletionSource<TArg?>();
            void SetResult( IActivityMonitor m, TSender sender, TArg item )
            {
                if( !predicate?.Invoke( item ) ?? false ) return;
                taskCompletionSource.SetResult( item );
                eventHandler.Remove( SetResult );
            }
            eventHandler.Add( SetResult );

            if( timeoutMillisecond < 0 )
            {
                return taskCompletionSource.Task;
            }
            Task.Delay( timeoutMillisecond ).ContinueWith( a =>
            {
                eventHandler.Remove( SetResult );
                taskCompletionSource.SetResult( null );
            } );
            return taskCompletionSource.Task;
        }

    }
}
