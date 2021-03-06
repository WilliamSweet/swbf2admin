﻿/*
 * This file is part of SWBF2Admin (https://github.com/jweigelt/swbf2admin). 
 * Copyright(C) 2017, 2018  Jan Weigelt <jan@lekeks.de>
 *
 * SWBF2Admin is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.

 * SWBF2Admin is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
 * GNU General Public License for more details.

 * You should have received a copy of the GNU General Public License
 * along with SWBF2Admin. If not, see<http://www.gnu.org/licenses/>.
 */
using System.Threading;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace SWBF2Admin.Scheduler
{
    public class TaskScheduler
    {
        public bool IsSync
        {
            get
            {
                return ((workThread != null) && workThread.ManagedThreadId == Thread.CurrentThread.ManagedThreadId);
            }
        }
        public int TickDelay { get; set; } = 10;
        private ConcurrentQueue<SchedulerTask> taskQueue;
        private List<RepeatingSchedulerTask> repeatingTasks;
        private List<RepeatingSchedulerTask> delayedTasks;

        private Thread workThread;
        private bool running;

        public TaskScheduler()
        {
            taskQueue = new ConcurrentQueue<SchedulerTask>();
            repeatingTasks = new List<RepeatingSchedulerTask>();
            delayedTasks = new List<RepeatingSchedulerTask>();
        }

        /// <summary>
        /// Starts the scheduler's workthread
        /// </summary>
        public void Start()
        {
            running = true;
            workThread = new Thread(this.DoWork);
            workThread.Start();
        }

        /// <summary>
        /// Stops the scheduler's workthread
        /// </summary>
        public void Stop()
        {
            running = false;
            if (workThread != null)
            {
                workThread.Join();
                workThread = null;
            }
        }

        /// <summary>
        /// Adds a task to the queue
        /// </summary>
        /// <param name="task">Task to be executed</param>
        public void PushTask(SchedulerTask task)
        {
            taskQueue.Enqueue(task);
        }

        /// <summary>
        /// Adds a task to the queue
        /// </summary>
        /// <param name="d">Delegate to be executed</param>
        public void PushTask(SchedulerTask.TaskDelegate d)
        {
            taskQueue.Enqueue(new SchedulerTask(d));
        }

        /// <summary>
        /// Adds a repeating task to the list
        /// </summary>
        /// <param name="task">Task to be executed</param>
        public void PushRepeatingTask(RepeatingSchedulerTask task)
        {
            taskQueue.Enqueue(task);
        }

        public void PushDelayedTask(SchedulerTask.TaskDelegate d, int interval)
        {
            PushRepeatingTask(new DelayedSchedulerTask(d, interval));
        }

        /// <summary>
        /// Adds a repeating task to the list
        /// </summary>
        /// <param name="task">Task to be executed</param>
        /// <param name="interval">Delay between running the task</param>
        public void PushRepeatingTask(SchedulerTask.TaskDelegate d, int interval)
        {
            PushRepeatingTask(new RepeatingSchedulerTask(d, interval));
        }

        /// <summary>
        /// Removes all repeating tasks
        /// </summary>
        public void ClearRepeatingTasks()
        {
            repeatingTasks.Clear();
        }

        /// <summary>
        /// Mainthread
        /// </summary>
        private void DoWork()
        {
            Stack<SchedulerTask> toRemove = new Stack<SchedulerTask>();
            while (running)
            {
                if (taskQueue.Count > 0)
                {
                    SchedulerTask t;
                    while (!taskQueue.TryDequeue(out t)) Thread.Sleep(TickDelay);
                    if (t.GetType() == typeof(DelayedSchedulerTask) || t.GetType() == typeof(RepeatingSchedulerTask))
                    {
                        RepeatingSchedulerTask dst = (RepeatingSchedulerTask)t;
                        dst.Tick();
                        if (!dst.Remove) repeatingTasks.Add(dst);
                    }
                    else
                    {
                        t.Run();
                    }
                }

                foreach (RepeatingSchedulerTask task in this.repeatingTasks)
                {
                    task.Tick();
                    if (task.Remove) toRemove.Push(task);
                }

                while (toRemove.Count > 0)
                {
                    repeatingTasks.Remove((RepeatingSchedulerTask)toRemove.Pop());
                }

                Thread.Sleep(TickDelay);
            }
        }

        ~TaskScheduler()
        {
            Stop();
        }

    }
}
