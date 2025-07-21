﻿using AdminShellNS;
using System;
using System.Collections.Generic;
using System.IO.BACnet;
using System.Threading.Tasks;
using Aas = AasCore.Aas3_0;

namespace AasxPluginAssetInterfaceDescription
{
    public class AidBacnetConnection : AidBaseConnection
    {
        public BacnetClient Client;
        private Dictionary<uint, BacnetAddress> DeviceAddresses = new Dictionary<uint, BacnetAddress>();
        public BacnetAddress deviceAddress;

        override public async Task<bool> Open()
        {
            try
            {
                Client = new BacnetClient();
                Client.OnIam += OnIamHandler;

                if (TimeOutMs >= 10)
                {
                    Client.Timeout = (int)TimeOutMs;
                }

                Client.Start();

                // Extract device ID from the URI
                uint deviceId = uint.Parse(TargetUri.Host);
                if (!DeviceAddresses.ContainsKey(deviceId))
                {
                    Client.WhoIs((int)deviceId, (int)deviceId);
                    await Task.Delay(1000);
                }
                if (!DeviceAddresses.TryGetValue(deviceId, out deviceAddress))
                {
                    return false;
                }

                await Task.Yield();
                return true;
            }
            catch (Exception)
            {
                Client = null;
                return false;
            }
        }

        private void OnIamHandler(BacnetClient sender, BacnetAddress adr, uint deviceId, uint maxAPDU, BacnetSegmentations segmentation, ushort vendorId)
        {
            // Store the device address from I-Am response
            DeviceAddresses[deviceId] = adr;
        }

        override public bool IsConnected()
        {
            return Client != null;
        }

        override public void Close()
        {
            // Dispose client
            if (Client != null)
            {
                Client.Dispose();
                Client = null;
            }
        }

        override public int UpdateItemValue(AidIfxItemStatus item)
        {
            int res = 0;
            if (item?.FormData?.Href?.HasContent() != true ||
                item.FormData.Bacv_useService?.HasContent() != true ||
                !IsConnected() ||
                Client == null)
            {
                return res;
            }
            try
            {

                var href = item.FormData.Href.TrimStart('/');
                string[] mainParts = href.Split('/');
                string[] objectParts = mainParts[0].Split(',');

                var objectType = (BacnetObjectTypes)int.Parse(objectParts[0]);
                uint instance = uint.Parse(objectParts[1]);
                BacnetObjectId objectId = new BacnetObjectId(objectType, instance);

                var propertyId = (BacnetPropertyIds)int.Parse(mainParts[1]);

                // READ operation
                if (item.FormData.Bacv_useService.Trim().ToLower() == "readproperty")
                {
                    try
                    {
                        IList<BacnetValue> values_r1 = new List<BacnetValue>();
                        bool result_r1 = Client.ReadPropertyRequest(deviceAddress, objectId, propertyId, out values_r1);
                        if (result_r1 && values_r1.Count > 0 && values_r1[0].Value != null)
                        {
                            item.Value = values_r1[0].Value.ToString();
                            NotifyOutputItems(item, item.Value);
                            res = 1;
                        }
                    }
                    catch (Exception)
                    {
                        return res;
                    }
                }

                // WRITE operation
                else if (item.FormData.Bacv_useService.Trim().ToLower() == "writeproperty")
                {
                    try
                    {
                        if (item.MapOutputItems != null)
                            foreach (var moi in item.MapOutputItems)
                            {
                                // valid?
                                if (moi?.MapRelation?.Second == null)
                                    continue;

                                // For literal payloads
                                else if (moi.MapRelation.SecondHint is Aas.Property prop)
                                {
                                    if (item.Value == "" || prop.Value == item.Value)
                                    {
                                        IList<BacnetValue> values_r2 = new List<BacnetValue>();
                                        bool result_r2 = Client.ReadPropertyRequest(deviceAddress, objectId, propertyId, out values_r2);
                                        if (result_r2 && values_r2.Count > 0 && values_r2[0].Value != null && prop.Value == item.Value)
                                        {
                                            item.Value = values_r2[0].Value.ToString();
                                            NotifyOutputItems(item, item.Value);
                                            res = 1;
                                        }
                                    }
                                    else
                                    {
                                        float staticValue = float.Parse(prop.Value);
                                        BacnetValue[] values_w = new BacnetValue[] { new BacnetValue(staticValue) };
                                        bool result_w = Client.WritePropertyRequest(deviceAddress, objectId, propertyId, values_w);
                                        if (result_w)
                                        {
                                            item.Value = values_w[0].Value?.ToString();
                                            NotifyOutputItems(item, item.Value);
                                            res = 1;
                                        }
                                    }
                                }
                            }
                    }
                    catch (Exception)
                    {
                        return res;
                    }
                }
            }
            catch (Exception)
            {
                return res;
            }
            return res;
        }
    }
}