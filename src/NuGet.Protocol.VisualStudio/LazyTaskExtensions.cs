// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGet.Protocol.VisualStudio
{
    public static class LazyTaskExtensions
    {
        public static Lazy<Task<U>> UpCast<T, U>(this Lazy<Task<T>> lazy) where T : U
        {
            return new Lazy<Task<U>>(() =>
           {
               var task = lazy.Value;

               return task.UpCast<T,U>();
           });
        }

        private static async Task<U> UpCast<T, U>(this Task<T> task) where T : U
        {
            return (U)await task;
        }
    }
}
