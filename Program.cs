namespace OneCom
{
    using OpenQA.Selenium;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.Serialization.Json;

    class Program
    {
        static private string gitFolder = @"C:\Users\Mattias\Documents\GitHub\";

        static void Main(string[] args)
        {
            try
            {
                if (args != null && args.Length > 0)
                {
                    gitFolder = args[0];
                }
                //WriteSettings(gitFolder, new LoginInfo
                //{
                //    UserName = "",
                //    Password = ""
                //});
                //return;

                // NOTE: PLEASE NOTE THAT OUR SYNC JOB NEEDS TO RUN BEFORE THIS PROGRAM IS CALLED!

                var folderBoard = gitFolder + "brfskagagard-styrelsen" + Path.DirectorySeparatorChar;
                var folderBoardExists = Directory.Exists(folderBoard);

                var apartments = GetApartments(gitFolder);

                Console.WriteLine(apartments.Count);

                var driverLocation = System.AppContext.BaseDirectory + "binaries" + Path.DirectorySeparatorChar;

                LoginInfo login = ReadSettings(gitFolder);

                using (var driver = new OpenQA.Selenium.PhantomJS.PhantomJSDriver(driverLocation))
                {
                    driver.Manage().Timeouts().ImplicitlyWait(new TimeSpan(0, 0, 30));

                    LoginUser(driver, login);

                    var list = GetEmails(driver);

                    foreach (var apartment in apartments)
                    {
                        var emails = apartment.Owners.Select(o => o.Email).Where(e => !string.IsNullOrEmpty(e)).ToList();
                        var forwardsToRemove = new List<string>();

                        var email = apartment.Number + "@brfskagagard.se";
                        var emailInfo = list.FirstOrDefault(i => i.Email == email);
                        if (emailInfo != null)
                        {
                            // temporarly store emails we should not forward to anymore
                            var forwards = new List<string>(emailInfo.ForwardAddresses);
                            foreach (var forward in forwards)
                            {
                                if (emails.Contains(forward))
                                {
                                    emails.Remove(forward);
                                }
                                else
                                {
                                    forwardsToRemove.Add(forward);
                                }
                            }

                            // add emails that was not already present
                            if (emails.Count > 0)
                            {
                                foreach (var item in emails)
                                {
                                    AddForwardEmail(driver, emailInfo, item);
                                }
                            }

                            // do the actuall removal of email(s)
                            foreach (var item in forwardsToRemove)
                            {
                                RemoveForwardEmail(driver, emailInfo, item);
                            }

                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                throw;
            }
        }

        private static List<Apartment> GetApartments(string gitFolder)
        {
            var apartments = new List<Apartment>();
            var folders = Directory.GetDirectories(gitFolder, "brfskagagard-lgh*");
            var json = new DataContractJsonSerializer(typeof(Apartment));
            foreach (string folder in folders)
            {
                using (
                    var stream =
                        File.OpenRead(folder + Path.DirectorySeparatorChar + "apartment.json"))
                {
                    stream.Position = 0;
                    var apartment = json.ReadObject(stream) as Apartment;
                    if (apartment == null)
                    {
                        continue;
                    }

                    apartments.Add(apartment);
                }
            }
            return apartments;
        }


        private static void RemoveForwardEmail(OpenQA.Selenium.PhantomJS.PhantomJSDriver driver, EmailInfo emailInfo, string forwardEmail)
        {
            driver.Navigate().GoToUrl(emailInfo.EditUrl);
            var rows = driver.FindElementsByCssSelector("#forward tbody tr,.forwards tbody tr");
            foreach (var row in rows)
            {
                var rowEmail = row.FindElement(By.CssSelector("td:first-child span")).Text;
                var rowRemoveLink = row.FindElement(By.CssSelector("a"));
                if (rowEmail == forwardEmail)
                {
                    rowRemoveLink.Click();

                    var submitBtn = driver.FindElementByCssSelector(".buttons input[type=submit].arrow-right");
                    submitBtn.Click();
                    return;
                }
            }
        }

        private static void AddForwardEmail(OpenQA.Selenium.PhantomJS.PhantomJSDriver driver, EmailInfo emailInfo, string forwardEmail)
        {
            var hasForwardEmail = emailInfo.ForwardAddresses.FirstOrDefault(f => f == forwardEmail) != null;
            if (hasForwardEmail)
            {
                return;
            }

            driver.Navigate().GoToUrl(emailInfo.EditUrl);
            var recipientElement = driver.FindElementById("addRecipient");
            recipientElement.SendKeys(forwardEmail);

            var AddBtn = driver.FindElementByCssSelector(".addForwardsRecipient input[type=submit]");
            AddBtn.Click();

            var submitBtn = driver.FindElementByCssSelector(".buttons input[type=submit].arrow-right");
            submitBtn.Click();
        }

        private static List<EmailInfo> GetEmails(OpenQA.Selenium.PhantomJS.PhantomJSDriver driver)
        {
            GoToEmailOverview(driver);

            var list = new List<EmailInfo>();

            // email accounts
            var elements = driver.FindElementsByCssSelector(".accounts tbody tr td:first-child span");
            foreach (var element in elements)
            {
                var email = element.Text.Trim();
                var name = email.Substring(0, email.IndexOf("@"));
                var editLink = "https://www.one.com/admin/edit-account.do?name=" + name;

                var info = new EmailInfo();
                info.Email = email;
                info.EditUrl = editLink;

                list.Add(info);
            }

            // aliases (we should probably use this one)
            elements = driver.FindElementsByCssSelector(".aliases .labeled td:first-child span");
            foreach (var element in elements)
            {
                var email = element.Text.Trim();
                var name = email.Substring(0, email.IndexOf("@"));
                var editLink = "https://www.one.com/admin/edit-alias.do?name=" + name;

                var info = new EmailInfo();
                info.Email = email;
                info.EditUrl = editLink;

                list.Add(info);
            }

            // add forward addresses
            foreach (var info in list)
            {
                driver.Navigate().GoToUrl(info.EditUrl);

                var forwardElements = driver.FindElementsByCssSelector(".forwards tr td:first-child span,#forward tbody tr td:first-child span");
                foreach (var item in forwardElements)
                {
                    info.ForwardAddresses.Add(item.Text.Trim());
                }
            }

            return list;
        }

        private static void GoToEmailOverview(OpenQA.Selenium.PhantomJS.PhantomJSDriver driver)
        {
            var emailLink = driver.FindElement(By.Id("frontpageMailLink"));
            emailLink.Click();
        }

        private static void LoginUser(OpenQA.Selenium.PhantomJS.PhantomJSDriver driver, LoginInfo login)
        {
            driver.Navigate().GoToUrl("https://login.one.com/cp/");
            var username = driver.FindElement(By.Name("displayUsername"));
            username.SendKeys(login.UserName);

            var password = driver.FindElement(By.ClassName("password"));
            password.SendKeys(login.Password);

            var button = driver.FindElement(By.ClassName("oneButton"));
            button.Click();
        }

        private static LoginInfo ReadSettings(string gitFolder)
        {
            var stream = System.IO.File.OpenRead(gitFolder + "one-setting.json");

            DataContractJsonSerializer serializer =
                new DataContractJsonSerializer(typeof(LoginInfo));

            var setting = serializer.ReadObject(stream) as LoginInfo;
            stream.Close();
            return setting;
        }

        private static void WriteSettings(string gitFolder, LoginInfo login)
        {
            var stream = System.IO.File.Create(gitFolder + "one-setting.json");

            DataContractJsonSerializer serializer =
                new DataContractJsonSerializer(typeof(LoginInfo));

            serializer.WriteObject(stream, login);
            stream.Flush();
            stream.Close();
        }
    }
}
