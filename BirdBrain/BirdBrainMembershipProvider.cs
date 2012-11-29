﻿using System;
using System.Collections.Specialized;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Security;
using Microsoft.Practices.ServiceLocation;
using Raven.Client;
using Raven.Client.Document;

namespace BirdBrain
{
    public class BirdBrainMembershipProvider : MembershipProvider
    {
        public const string ProviderName = "BirdBrainMembership";

        private DocumentStore documentStore;
        private int minRequiredPasswordLength = 6;
        private int maxInvalidPasswordAttempts = 5;
        private int minRequiredNonAlphanumericCharacters = 0;
        private int passwordAttemptWindow = 1;
        private MembershipPasswordFormat passwordFormat = MembershipPasswordFormat.Hashed;
        private string passwordStrengthRegularExpression = "[\\d\\w].*";
        private bool requiresQuestionAndAnswer = true;

        public override string ApplicationName { get; set; }

        public override void Initialize(string name, NameValueCollection config)
        {
            base.Initialize(name, config);
            if (config["minRequiredPasswordLength"] != null)
            {
                minRequiredPasswordLength = Int32.Parse(config["minRequiredPasswordLength"]);
            }
            if (config["maxInvalidPasswordAttempts"] != null)
            {
                maxInvalidPasswordAttempts = Int32.Parse(config["maxInvalidPasswordAttempts"]);
            }
            if (config["minRequiredNonAlphanumericCharacters"] != null)
            {
                minRequiredNonAlphanumericCharacters = Int32.Parse(config["minRequiredNonAlphanumericCharacters"]);
            }
            if (config["passwordAttemptWindow"] != null)
            {
                passwordAttemptWindow = Int32.Parse(config["passwordAttemptWindow"]);
            }
            if (config["passwordFormat"] != null)
            {
                MembershipPasswordFormat _passwordFormat;
                if (Enum.TryParse(config["passwordFormat"], true, out _passwordFormat))
                {
                    passwordFormat = _passwordFormat;
                }
            }
            if (config["passwordStrengthRegularExpression"] != null)
            {
                passwordStrengthRegularExpression = config["passwordStrengthRegularExpression"];
            }
            if (config["requiresQuestionAndAnswer"] != null)
            {
                requiresQuestionAndAnswer = Boolean.Parse(config["requiresQuestionAndAnswer"]);
            }
            documentStore = ServiceLocator.Current.GetInstance<DocumentStore>();
        }

        private static string HashPassword(string password)
        {
            var encoder = new UTF8Encoding();
            var hashedPassword = encoder.GetString(SHA1.Create().ComputeHash(encoder.GetBytes(password)));
            return hashedPassword;
        }

        private static User GetUserByUsernameAndPassword(string username, string password, IDocumentSession session)
        {
            var usersQuery = from user in session.Query<User>()
                          where user.Username == username &&
                                user.Password == password
                          select user;
            var users = usersQuery.ToArray();
            return users.Count() != 0 ? users.First() : null;
        }

        private static User GetUserByUsername(string username, IDocumentSession session)
        {
            var usersQuery = from user in session.Query<User>()
                          where user.Username == username
                          select user;
            var users = usersQuery.ToArray();
            return users.Count() != 0 ? users.First() : null;
        }

        private static User GetUserByUsernameAndAnswer(string username, string answer, IDocumentSession session)
        {
            var usersQuery = from user in session.Query<User>()
                             where user.Username == username &&
                                   user.PasswordAnswer == answer
                             select user;
            var users = usersQuery.ToArray();
            return users.Count() != 0 ? users.First() : null;
        }

        public override bool ChangePassword(string username, string oldPassword, string newPassword)
        {
            using (var session = documentStore.OpenSession())
            {
                var user = GetUserByUsernameAndPassword(username, HashPassword(oldPassword), session);
                if (user != null)
                {
                    user.Password = HashPassword(newPassword);
                    session.Store(user);
                    session.SaveChanges();
                    return true;
                }
                return false;
            }
        }

        public override bool ChangePasswordQuestionAndAnswer(string username, string password, string newPasswordQuestion, string newPasswordAnswer)
        {
            using (var session = documentStore.OpenSession())
            {
                var user = GetUserByUsernameAndPassword(username, HashPassword(password), session);
                if (user != null)
                {
                    user.PasswordQuestion = newPasswordQuestion;
                    user.PasswordAnswer = newPasswordAnswer;
                    session.Store(user);
                    session.SaveChanges();
                    return true;
                }
                return false;
            }
        }

        public override MembershipUser CreateUser(string username, string password, string email, string passwordQuestion, string passwordAnswer, bool isApproved, object providerUserKey, out MembershipCreateStatus status)
        {
            var user = new User(username, HashPassword(password), email, passwordQuestion, passwordAnswer);
            using (var session = documentStore.OpenSession())
            {
                session.Store(user);
                session.SaveChanges();
                status = MembershipCreateStatus.Success;
                return new BirdBrainMembershipUser(user);
            }
        }

        public override bool DeleteUser(string username, bool deleteAllRelatedData)
        {
            using (var session = documentStore.OpenSession())
            {
                var user = GetUserByUsername(username, session);
                if (user != null)
                {
                    session.Delete(user);
                    session.SaveChanges();
                    return true;
                }
                return false;
            }
        }

        public override bool EnablePasswordReset
        {
            get { return true; }
        }

        public override bool EnablePasswordRetrieval
        {
            get { return false; }
        }

        public override MembershipUserCollection FindUsersByEmail(string emailToMatch, int pageIndex, int pageSize, out int totalRecords)
        {
            using (var session = documentStore.OpenSession())
            {
                var results = from _user in session.Query<User>()
                              where _user.Email == emailToMatch
                              select _user;
                totalRecords = results.Count();
                var users = new MembershipUserCollection();
                foreach (var user in results)
                {
                    users.Add(new BirdBrainMembershipUser(user));
                }
                return users;
            }
        }

        public override MembershipUserCollection FindUsersByName(string usernameToMatch, int pageIndex, int pageSize, out int totalRecords)
        {
            using (var session = documentStore.OpenSession())
            {
                var results = from _user in session.Query<User>()
                              where _user.Username == usernameToMatch
                              select _user;
                totalRecords = results.Count();
                var users = new MembershipUserCollection();
                foreach (var user in results)
                {
                    users.Add(new BirdBrainMembershipUser(user));
                }
                return users;
            }
        }

        public override MembershipUserCollection GetAllUsers(int pageIndex, int pageSize, out int totalRecords)
        {
            using (var session = documentStore.OpenSession())
            {
                var results = session.Query<User>().Select(_user => _user).Skip((pageIndex - 1)*pageSize).Take(pageSize);
                totalRecords = results.Count();
                var membershipUsers = new MembershipUserCollection();
                foreach (var user in results)
                {
                    membershipUsers.Add(new BirdBrainMembershipUser(user));
                }
                return membershipUsers;
            }
        }

        public override int GetNumberOfUsersOnline()
        {
            using (var session = documentStore.OpenSession())
            {
                var results = from _user in session.Query<User>()
                              where _user.LastActive >= DateTime.Now.Subtract(TimeSpan.FromMinutes(Membership.UserIsOnlineTimeWindow))
                              select _user;
                return results.Count();
            }
        }

        public override string GetPassword(string username, string answer)
        {
            return null;
        }

        public override MembershipUser GetUser(string username, bool userIsOnline)
        {
            using (var session = documentStore.OpenSession())
            {
                var user = GetUserByUsername(username, session);
                if (user != null)
                {
                    if (userIsOnline)
                    {
                        user.LastActive = DateTime.Now;
                        session.Store(user);
                        session.SaveChanges();
                    }
                    return new BirdBrainMembershipUser(user);
                }
                return null;
            }
        }

        public override MembershipUser GetUser(object providerUserKey, bool userIsOnline)
        {
            using (var session = documentStore.OpenSession())
            {
                var user = session.Load<User>(providerUserKey.ToString());
                return new BirdBrainMembershipUser(user);
            }
        }

        public override string GetUserNameByEmail(string email)
        {
            using (var session = documentStore.OpenSession())
            {
                var usersQuery = from _user in session.Query<User>()
                                 where _user.Email == email
                                 select _user.Username;
                return usersQuery.ToArray().First();
            }
        }

        public override int MaxInvalidPasswordAttempts
        {
            get { return maxInvalidPasswordAttempts; }
        }

        public override int MinRequiredNonAlphanumericCharacters
        {
            get { return minRequiredNonAlphanumericCharacters; }
        }

        public override int MinRequiredPasswordLength
        {
            get { return minRequiredPasswordLength; }
        }

        public override int PasswordAttemptWindow
        {
            get { return passwordAttemptWindow; }
        }

        public override MembershipPasswordFormat PasswordFormat
        {
            get { return passwordFormat; }
        }

        public override string PasswordStrengthRegularExpression
        {
            get { return passwordStrengthRegularExpression; }
        }

        public override bool RequiresQuestionAndAnswer
        {
            get { return requiresQuestionAndAnswer; }
        }

        public override bool RequiresUniqueEmail
        {
            get { return true; }
        }

        public override string ResetPassword(string username, string answer)
        {
            using (var session = documentStore.OpenSession())
            {
                var user = GetUserByUsernameAndAnswer(username, answer, session);
                if (user != null)
                {
                    var password = Membership.GeneratePassword(MinRequiredPasswordLength, MinRequiredNonAlphanumericCharacters);
                    user.Password = HashPassword(password);
                    session.Store(user);
                    session.SaveChanges();
                    return password;
                }
                throw new MembershipPasswordException("Unable to reset password.");
            }
        }

        public override bool UnlockUser(string userName)
        {
            return true;
        }

        public override void UpdateUser(MembershipUser user)
        {
            using (var session = documentStore.OpenSession())
            {
                var existingUser = session.Load<User>(user.ProviderUserKey.ToString());
                if (existingUser != null)
                {
                    existingUser.Email = user.Email;
                    existingUser.IsApproved = user.IsApproved;
                    existingUser.LastLogin = user.LastLoginDate;
                    existingUser.LastActive = user.LastActivityDate;
                    existingUser.Comment = user.Comment;
                    session.Store(existingUser);
                    session.SaveChanges();
                }
            }
        }

        public override bool ValidateUser(string username, string password)
        {
            using (var session = documentStore.OpenSession())
            {
                var user = GetUserByUsernameAndPassword(username, HashPassword(password), session);
                if (user != null)
                {
                    user.LastActive = DateTime.Now;
                    user.LastLogin = DateTime.Now;
                    session.Store(user);
                    session.SaveChanges();
                    return true;
                }
                return false;
            }
        }
    }
}
