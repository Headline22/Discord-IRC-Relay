using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Serialization;

namespace IRCRelay.Config
{
    public class XmlSerializable<T>
        where T : XmlSerializable<T>
    {
        public static T Load(string fileName)
        {
            FileStream fs = null;
            T ret = null;
            try
            {
                fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
                XmlSerializer xs = new XmlSerializer(typeof(T));
                ret = (T)xs.Deserialize(fs);
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if (fs != null)
                    fs.Close();
            }

            return ret;
        }

        public static bool Save(string fileName, T data)
        {
            FileStream fs = null;
            try
            {
                fs = File.Create(fileName);
                XmlSerializer xs = new XmlSerializer(typeof(T));
                xs.Serialize(fs, data);

                return true;
            }
            catch
            {
                throw;
            }
            finally
            {
                if (fs != null)
                fs.Close();
            }
        }

        public bool Save(string fileName)
        {
            if (this is T s)
            {
                return Save(fileName, s);
            }
            else
            {
                return false;
            }
        }
    }
}