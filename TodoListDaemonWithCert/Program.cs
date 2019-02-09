﻿/*
 The MIT License (MIT)

Copyright (c) 2018 Microsoft Corporation

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
 */

// The following using statements were added for this sample.
using System;
using System.Threading;

namespace TodoListDaemonWithCert
{
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                AuthenticationHelper authenticationHelper = new AuthenticationHelper();
                CallApiHelper callApiHelper = new CallApiHelper(authenticationHelper);

                // Call the ToDoList service 10 times with short delay between calls.
                int delay = 1000;
                for (int i = 0; i < 10; i++)
                {
                    callApiHelper.PostNewTodoItem().Wait();
                    Thread.Sleep(delay);
                    callApiHelper.DisplayTodoList().Wait();
                    Thread.Sleep(delay);
                }

                Console.WriteLine("Press any key to continue");
                Console.ReadKey();
            }
            catch(AuthenticationHelperException authenticationHelperException)
            {
                return authenticationHelperException.ErrorCode;
            }
            catch
            {
                return -2;
            }

            return 0;
        }
    }
}
