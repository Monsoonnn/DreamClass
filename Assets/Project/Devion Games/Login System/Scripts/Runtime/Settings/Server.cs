using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DevionGames.LoginSystem.Configuration
{
    [System.Serializable]
    public class Server : Settings
    {
        public override string Name
        {
            get
            {
                return "Server";
            }
        }


        [Header("Server Settings:")]
        public string serverAddress = "http://localhost:3000/api/auth";
        public string createAccount = "register";
        public string login = "login";
        public string recoverPassword = "";
        public string resetPassword = "";
        public string accountKey = "Account";

    }
}