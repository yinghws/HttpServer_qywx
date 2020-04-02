using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Data.SqlClient;
using System.Collections;

namespace ConsoleApplication1
{

    class Http_send_cardmessage
    {
        public class Message_btn
        {
            public enum color
            {
                red,
                blue
            }
            public string key;
            public string name;
            public string replace_name;
            public color btncolor;
            public bool bold;
        }
        public string title;
        public string description;
        public string url;
        public string task_id;
        public Message_btn[] btn;



        public string get_btnstring()
        {
            string ret = "";
            for (int i = 0; i < btn.Length; i++)
            {
                if (i == 0)
                {
                    // ret = "{";

                }
                else
                {
                    ret = ret + ",";
                }
                ret = ret + string.Format("{{\"key\": \"{0}\",\"name\": \"{1}\",\"replace_name\": \"{2}\",\"color\":\"{3}\",\"is_bold\": {4}}}", "btn" + i.ToString(), btn[i].name, btn[i].replace_name, btn[i].btncolor, btn[i].bold.ToString().ToLower());



            }
            return ret;
        }

    }
    class Http_send
    {
        private static string token;
        private static DateTime gettime;
        private static bool firstget = true;
        private HttpClient http;

        public Http_send()
        {
            http = new HttpClient();
        }


        public bool Is_holiday(string date1)
        {
            string url = string.Format("http://www.easybots.cn/api/holiday.php?d={0}",date1);
            var responseString = http.GetStringAsync(url);
            JObject jztxx = JObject.Parse(responseString.Result);
            string result = jztxx[date1].ToString();
            if (result == "0")
            {
                return false;
            }
            else
            {
                return true;
            }
        }
        public string Get_token()
        {
            if ((firstget) || (gettime.Subtract(DateTime.Now).TotalSeconds < -7200))
            {
                string url = "https://qyapi.weixin.qq.com/cgi-bin/gettoken?corpid=wwf0b1680d1b942f7b&corpsecret=yQ6u6b7JzZyznCD45F_0UPyJU9bx8_2mYEF34jTy7T0";
                var responseString = http.GetStringAsync(url);
                JObject jztxx = JObject.Parse(responseString.Result);
                token = jztxx["access_token"].ToString();
                gettime = DateTime.Now;
                firstget = false;
            }
            return token;
        }

        public void App_send_card(string x_name, string x_detp, string x_label, Http_send_cardmessage x_message)
        {
            string x_content = string.Format("{{\"touser\" : \"{0}\",\"toparty\" : \"{1}\",\"totag\" : \"{2}\",\"msgtype\" : \"taskcard\",\"agentid\" : 1000004,\"taskcard\" : {{\"title\" : \"{3}\",\"description\" : \"{4}\",\"url\" : \"{5}\",\"task_id\" : \"{6}\",\"btn\":[{7}]}},\"enable_id_trans\": 0,\"enable_duplicate_check\": 0}}", x_name, x_detp, x_label, x_message.title, x_message.description, x_message.url, x_message.task_id, x_message.get_btnstring());
            HttpContent a = new StringContent(x_content, Encoding.UTF8, "application/json");
            var s = http.PostAsync("https://qyapi.weixin.qq.com/cgi-bin/message/send?access_token=" + Get_token(), a);
            HttpResponseMessage zz = s.Result;
            JObject j_jsxx = JObject.Parse(zz.Content.ReadAsStringAsync().Result);
            App_send_log("https://qyapi.weixin.qq.com/cgi-bin/message/send?access_token=" + Get_token(), x_content, j_jsxx.ToString());
            if (j_jsxx["errcode"].ToString() != "0")
            {
                //return j_jsxx["errmsg"].ToString();
                x_content = string.Format("{{\"touser\" : \"{0}\",\"toparty\" : \"{1}\",\"totag\" : \"{2}\",\"msgtype\" : \"text\",\"agentid\" : 1000004,\"text\" : {{\"content\" : \"{3}\"}},\"safe\":0,\"enable_id_trans\": 0,\"enable_duplicate_check\": 0}}", "", "2", "", "推送出错：" + j_jsxx["errmsg"].ToString());
                a = new StringContent(x_content, Encoding.UTF8, "application/json");
                http.PostAsync("https://qyapi.weixin.qq.com/cgi-bin/message/send?access_token=" + Get_token(), a);
            }
        }

        public void App_send(string x_name, string x_detp, string x_label, string x_message, string x_safe = "0")
        {
            string x_content = string.Format("{{\"touser\" : \"{0}\",\"toparty\" : \"{1}\",\"totag\" : \"{2}\",\"msgtype\" : \"text\",\"agentid\" : 1000004,\"text\" : {{\"content\" : \"{3}\"}},\"safe\":{4},\"enable_id_trans\": 0,\"enable_duplicate_check\": 0}}", x_name, x_detp, x_label, x_message, x_safe);

            HttpContent a = new StringContent(x_content, Encoding.UTF8, "application/json");

            var s = http.PostAsync("https://qyapi.weixin.qq.com/cgi-bin/message/send?access_token=" + Get_token(), a);

            HttpResponseMessage zz = s.Result;
            JObject j_jsxx = JObject.Parse(zz.Content.ReadAsStringAsync().Result);
            App_send_log("https://qyapi.weixin.qq.com/cgi-bin/message/send?access_token=" + Get_token(), x_content, j_jsxx.ToString());
            if (j_jsxx["errcode"].ToString() != "0")
            {
                //return j_jsxx["errmsg"].ToString();
                x_content = string.Format("{{\"touser\" : \"{0}\",\"toparty\" : \"{1}\",\"totag\" : \"{2}\",\"msgtype\" : \"text\",\"agentid\" : 1000004,\"text\" : {{\"content\" : \"{3}\"}},\"safe\":0,\"enable_id_trans\": 0,\"enable_duplicate_check\": 0}}", "", "2", "", "推送出错：" + j_jsxx["errmsg"].ToString());
                a = new StringContent(x_content, Encoding.UTF8, "application/json");
                http.PostAsync("https://qyapi.weixin.qq.com/cgi-bin/message/send?access_token=" + Get_token(), a);
            }

            if (j_jsxx["invaliduser"] != null && j_jsxx["invaliduser"].ToString() != "")
            {
                x_content = string.Format("{{\"touser\" : \"{0}\",\"toparty\" : \"{1}\",\"totag\" : \"{2}\",\"msgtype\" : \"text\",\"agentid\" : 1000004,\"text\" : {{\"content\" : \"{3}\"}},\"safe\":0,\"enable_id_trans\": 0,\"enable_duplicate_check\": 0}}", "", "2", "", "未推送成功联系人：" + j_jsxx["invaliduser"].ToString());
                a = new StringContent(x_content, Encoding.UTF8, "application/json");
                http.PostAsync("https://qyapi.weixin.qq.com/cgi-bin/message/send?access_token=" + Get_token(), a);
            }
            if (j_jsxx["invalidparty"] != null && j_jsxx["invalidparty"].ToString() != "")
            {
                x_content = string.Format("{{\"touser\" : \"{0}\",\"toparty\" : \"{1}\",\"totag\" : \"{2}\",\"msgtype\" : \"text\",\"agentid\" : 1000004,\"text\" : {{\"content\" : \"{3}\"}},\"safe\":0,\"enable_id_trans\": 0,\"enable_duplicate_check\": 0}}", "", "2", "", "未推送成功部门：" + j_jsxx["invalidparty"].ToString());
                a = new StringContent(x_content, Encoding.UTF8, "application/json");
                http.PostAsync("https://qyapi.weixin.qq.com/cgi-bin/message/send?access_token=" + Get_token(), a);
            }
            if (j_jsxx["invalidtag"] != null && j_jsxx["invalidtag"].ToString() != "")
            {
                x_content = string.Format("{{\"touser\" : \"{0}\",\"toparty\" : \"{1}\",\"totag\" : \"{2}\",\"msgtype\" : \"text\",\"agentid\" : 1000004,\"text\" : {{\"content\" : \"{3}\"}},\"safe\":0,\"enable_id_trans\": 0,\"enable_duplicate_check\": 0}}", "", "2", "", "未推送成功联系人组：" + j_jsxx["invalidtag"].ToString());
                a = new StringContent(x_content, Encoding.UTF8, "application/json");
                http.PostAsync("https://qyapi.weixin.qq.com/cgi-bin/message/send?access_token=" + Get_token(), a);
            }
        }


        public void App_send_group(string x_group_id, string x_message)
        {
            string x_content = string.Format("{{\"chatid\": \"{0}\",\"msgtype\":\"text\",\"text\":{{\"content\" : \"{1}\"}},\"safe\":0}}", x_group_id, x_message);

            HttpContent a = new StringContent(x_content, Encoding.UTF8, "application/json");
            var s = http.PostAsync("https://qyapi.weixin.qq.com/cgi-bin/appchat/send?access_token=" + Get_token(), a);
            HttpResponseMessage zz = s.Result;
            JObject j_jsxx = JObject.Parse(zz.Content.ReadAsStringAsync().Result);
            App_send_log("https://qyapi.weixin.qq.com/cgi-bin/appchat/send?access_token=" + Get_token(), x_content, j_jsxx.ToString());
            if (j_jsxx["errcode"].ToString() != "0")
            {
                //return j_jsxx["errmsg"].ToString();
                x_content = string.Format("{{\"touser\" : \"{0}\",\"toparty\" : \"{1}\",\"totag\" : \"{2}\",\"msgtype\" : \"text\",\"agentid\" : 1000004,\"text\" : {{\"content\" : \"{3}\"}},\"safe\":0,\"enable_id_trans\": 0,\"enable_duplicate_check\": 0}}", "", "2", "", "推送出错：" + j_jsxx["errmsg"].ToString());
                a = new StringContent(x_content, Encoding.UTF8, "application/json");
                http.PostAsync("https://qyapi.weixin.qq.com/cgi-bin/message/send?access_token=" + Get_token(), a);
            }
        }

        public void App_send_log(string x_send_http, string x_send_message, string x_send_result)
        {
            SqlConnection con_writelog = new SqlConnection();
            con_writelog.ConnectionString = "server=192.168.8.18;database=my_data;user=sa;pwd=VA4X1abfy76pY";
            con_writelog.Open();
            SqlCommand commd_sql = new SqlCommand();
            commd_sql.Connection = con_writelog;
            commd_sql.CommandText = string.Format("insert into qywx_sendlog (send_http,send_message,send_result,send_date) values ('{0}','{1}','{2}',GETDATE())", x_send_http, x_send_message, x_send_result);
            commd_sql.ExecuteNonQuery();
            con_writelog.Close();

        }


        public void Sort_dept()
        {
            SqlConnection conn = new SqlConnection();
            conn.ConnectionString = "server=172.22.52.51;database=master;user=snzyy;pwd=Snzyy123.";
            conn.Open();
            SqlCommand commd = new SqlCommand();
            commd.Connection = conn;
            commd.CommandText = "select DEPT_ID,DEPT_NAME from COMM.COMM.DEPTS where DEPT_ID in (select DEPT_ID from MZ.OUT.REGISTERS where REGISTER_TIME > DATEADD(MONTH, -1, GETDATE()) and FLAG_INVALID = 0) or DEPT_CODE in (sELECT DEPT_CODE from zy.[IN].PAT_ALL_INFO_VIEW where PAT_IN_TIME> DATEADD(MONTH, -1, GETDATE()) and PAT_IN_HOS_CODE<>'')";
            SqlDataReader qry_pub = commd.ExecuteReader();
            Dictionary<string, string> d_dept = new Dictionary<string, string>();
            SqlConnection conn_oldhis = new SqlConnection();
            conn_oldhis.ConnectionString = "server=192.168.8.18;database=my_data;user=sa;pwd=VA4X1abfy76pY";
            conn_oldhis.Open();
            SqlCommand commd_oldhis = new SqlCommand();
            commd_oldhis.Connection = conn_oldhis;
            SqlDataReader qry_oldhis;
            while (qry_pub.Read())
            {
                commd_oldhis.CommandText = string.Format("select DEPT_ID from qywx_sort_dept where dept_id='{0}'", qry_pub["DEPT_ID"].ToString());
                qry_oldhis = commd_oldhis.ExecuteReader();
                if (!qry_oldhis.HasRows)
                {
                    qry_oldhis.Close();
                    commd_oldhis.CommandText = string.Format("insert into qywx_sort_dept (DEPT_ID,DEPT_NAME) values ('{0}','{1}')", qry_pub["DEPT_ID"].ToString(), qry_pub["DEPT_NAME"].ToString());
                    commd_oldhis.ExecuteNonQuery();
                }
                qry_oldhis.Close();
            }
            qry_pub.Close();

            commd_oldhis.CommandText = "select DEPT_id,isnull(DEPT_sort,'未确定') part from qywx_sort_dept";
            qry_oldhis = commd_oldhis.ExecuteReader();
            while (qry_oldhis.Read())
            {
                d_dept.Add(qry_oldhis["DEPT_id"].ToString(), qry_oldhis["part"].ToString());
            }
            commd.CommandText = string.Format("SELECT  A.DEPT_ID,ISNULL(MAX(B.DEPT_NAME), '未指定') AS 科室名称 , COUNT(DISTINCT A.REGISTER_ID) AS 就诊人次, SUM(CASE WHEN A.FLAG_CHARGE=0 OR a.REGISTER_CLASS_ID=11 THEN 0 ELSE 1 END )挂号人次 FROM    MZ.OUT.REGISTERS A WITH ( NOLOCK ) LEFT JOIN COMM.COMM.DEPTS AS B WITH ( NOLOCK ) ON A.DEPT_ID = B.DEPT_ID INNER JOIN COMM.DICT.REGISTER_CLASSES AS C WITH ( NOLOCK ) ON A.REGISTER_CLASS_ID = C.REGISTER_CLASS_ID WHERE A.REGISTER_TIME >= '{0}' AND  A.REGISTER_TIME <='{1}' AND A.FLAG_INVALID = 0 GROUP BY A.DEPT_ID order by COUNT(DISTINCT A.REGISTER_ID) desc", DateTime.Now.AddDays(-1).ToShortDateString() + " 00:00:00", DateTime.Now.AddDays(-1).ToShortDateString() + " 23:59:59");
            qry_pub = commd.ExecuteReader();
            string x_mzpxb_head = "", x_mzpxb_body = "";
            int yq_tfj = 0, yq_hpl = 0, yq_hj = 0;
            while (qry_pub.Read())
            {
                x_mzpxb_body = x_mzpxb_body + Zfc_modify(qry_pub["科室名称"].ToString()) + ":" + qry_pub["就诊人次"].ToString() + d_dept[qry_pub["DEPT_ID"].ToString()] + "\n";

                yq_hj = yq_hj + Convert.ToInt32(qry_pub["就诊人次"].ToString());
                if (d_dept[qry_pub["DEPT_ID"].ToString()].IndexOf("天峰街") > -1)
                {
                    yq_tfj = yq_tfj + Convert.ToInt32(qry_pub["就诊人次"].ToString());
                }

                if (d_dept[qry_pub["DEPT_ID"].ToString()].IndexOf("和平路") > -1)
                {
                    yq_hpl = yq_hpl + Convert.ToInt32(qry_pub["就诊人次"].ToString());
                }

                if (d_dept[qry_pub["DEPT_ID"].ToString()].IndexOf("未确定") > -1)
                {
                    x_mzpxb_head = "（存在未确定院区的科室）";
                }


            }

            x_mzpxb_head = DateTime.Now.AddDays(-1).ToLongDateString() + " 不包含步行街 " + x_mzpxb_head + "\n";
            x_mzpxb_head = x_mzpxb_head + string.Format("挂号合计:{0}(和平路:{1} ; 天峰街:{2};) \n", yq_hj.ToString(), yq_hpl.ToString(), yq_tfj.ToString());

            App_send("", "2", "", "测试仅信息科收到\n" + x_mzpxb_head + x_mzpxb_body, "1");

            conn.Close();
            conn_oldhis.Close();

        }

        public string Zfc_modify(string x)
        {
            if (x.IndexOf("(") > -1)
            {
                return x.Substring(0, x.IndexOf("("));
            }
            else
            {
                if (x.IndexOf("（") > -1)
                {
                    return x.Substring(0, x.IndexOf("（"));
                }
                else
                    return x;
            }
        }

        public void dept_user_get()
        {
            string url = string.Format("https://qyapi.weixin.qq.com/cgi-bin/department/list?access_token={0}&id=1", Get_token());
            var responseString = http.GetStringAsync(url);
            JObject jztxx = JObject.Parse(responseString.Result);
            if (jztxx["errcode"].ToString() == "0")
            {
                JArray data = JArray.Parse(jztxx["department"].ToString());
                SqlConnection conn = new SqlConnection();
                conn.ConnectionString = "server=192.168.8.18;database=my_data;user=sa;pwd=VA4X1abfy76pY";
                conn.Open();
                SqlCommand cmd = new SqlCommand();
                cmd.Connection = conn;
                cmd.CommandType = System.Data.CommandType.Text;
                foreach (JObject m in data)
                {
                    cmd.CommandText = string.Format("delete qywx_dept where id='{0}'", m["id"].ToString());
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = string.Format("insert into qywx_dept (id,name,parentid) values ()");
                }
            }
        }

    }
}
