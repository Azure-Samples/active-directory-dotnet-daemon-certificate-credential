/*
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

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace TodoListDaemonWithCert
{
    public class CallApiHelper
    {
        private HttpClient httpClient = new HttpClient();
        private AuthenticationHelper AuthenticationHelper = null;

        private CallApiHelper()
        { }

        public CallApiHelper(AuthenticationHelper authenticationHelper)
        {
            if (authenticationHelper == null)
            {
                throw new ArgumentNullException(nameof(authenticationHelper));
            }

            AuthenticationHelper = authenticationHelper;
        }

        /// <summary>
        /// Post a Todo item to the TodoList service
        /// </summary>
        public async Task PostNewTodoItem()
        {
            // Get a token
            var result = await AuthenticationHelper.GetAccessToken(ConfigurationHelper.todoListResourceId);
            if (result.AdalResult == null && result.MsalResult == null)
            {
                Console.WriteLine("AT retrieved through both ADAL and MSAL are null. Canceling attempt to contact To Do list service.\n");
                return;
            }

            if (result.AdalResult != null)
            {
                // Add the access token to the authorization header of the request.
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AdalResult.AccessToken);

                // Posts a new item by calling the API using the http client
                string timeNow = DateTime.Now.ToString();
                string todoText = "ADAL: Task at time: " + timeNow;

                // Forms encode To Do item and POST to the todo list web api.
                Console.WriteLine("ADAL: Posting to To Do list at {0}", timeNow);
                HttpContent content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("Title", todoText) });
                HttpResponseMessage response = await httpClient.PostAsync(ConfigurationHelper.todoListBaseAddress + "/api/todolist", content);
                if (response.IsSuccessStatusCode == true)
                {
                    Console.WriteLine("ADAL: Successfully posted new To Do item:  {0}\n", todoText);
                }
                else
                {
                    Console.WriteLine("ADAL: Failed to post a new To Do item\nError:  {0}\n", response.ReasonPhrase);
                }
            }

            if (result.MsalResult != null)
            {
                // Add the access token to the authorization header of the request.
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.MsalResult.AccessToken);

                // Posts a new item by calling the API using the http client
                string timeNow = DateTime.Now.ToString();
                string todoText = "MSAL: Task at time: " + timeNow;

                // Forms encode To Do item and POST to the todo list web api.
                Console.WriteLine("MSAL: Posting to To Do list at {0}", timeNow);
                HttpContent content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("Title", todoText) });
                HttpResponseMessage response = await httpClient.PostAsync(ConfigurationHelper.todoListBaseAddress + "/api/todolist", content);
                if (response.IsSuccessStatusCode == true)
                {
                    Console.WriteLine("MSAL: Successfully posted new To Do item:  {0}\n", todoText);
                }
                else
                {
                    Console.WriteLine("MSAL: Failed to post a new To Do item\nError:  {0}\n", response.ReasonPhrase);
                }
            }
        }

        /// <summary>
        /// Display the list of todo items by querying the todolist service
        /// </summary>
        /// <returns></returns>
        public async Task DisplayTodoList()
        {
            var result = await AuthenticationHelper.GetAccessToken(ConfigurationHelper.todoListResourceId);

            if (result.AdalResult != null)
            {
                // Add the access token to the authorization header of the request.
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AdalResult.AccessToken);

                // Call the To Do list service.
                Console.WriteLine("ADAL: Retrieving To Do list at {0}", DateTime.Now.ToString());
                HttpResponseMessage response = await httpClient.GetAsync(ConfigurationHelper.todoListBaseAddress + "/api/todolist");

                if (response.IsSuccessStatusCode)
                {
                    // Read the response and output it to the console.
                    string s = await response.Content.ReadAsStringAsync();
                    List<TodoItem> toDoArray = JsonConvert.DeserializeObject<List<TodoItem>>(s);
                    foreach (TodoItem item in toDoArray)
                    {
                        Console.WriteLine(item.Title);
                    }

                    Console.WriteLine("ADAL: Total item count:  {0}\n", toDoArray.Count);
                }
                else
                {
                    Console.WriteLine("ADAL: Failed to retrieve To Do list\nError:  {0}\n", response.ReasonPhrase);
                }
            }

            if (result.MsalResult != null)
            {
                // Add the access token to the authorization header of the request.
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.MsalResult.AccessToken);

                // Call the To Do list service.
                Console.WriteLine("MSAL: Retrieving To Do list at {0}", DateTime.Now.ToString());
                HttpResponseMessage response = await httpClient.GetAsync(ConfigurationHelper.todoListBaseAddress + "/api/todolist");

                if (response.IsSuccessStatusCode)
                {
                    // Read the response and output it to the console.
                    string s = await response.Content.ReadAsStringAsync();
                    List<TodoItem> toDoArray = JsonConvert.DeserializeObject<List<TodoItem>>(s);
                    foreach (TodoItem item in toDoArray)
                    {
                        Console.WriteLine(item.Title);
                    }

                    Console.WriteLine("MSAL: Total item count:  {0}\n", toDoArray.Count);
                }
                else
                {
                    Console.WriteLine("MSAL: Failed to retrieve To Do list\nError:  {0}\n", response.ReasonPhrase);
                }
            }
        }
    }
}

