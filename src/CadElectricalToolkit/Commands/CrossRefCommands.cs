using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using CadElectricalToolkit.CadAccess;
using CadElectricalToolkit.Core;

namespace CadElectricalToolkit.Commands
{
    /// <summary>
    /// Các lệnh tham chiếu tiếp điểm & cuộn hút Rơ-le/Contactor/Timer (TCCTB, TKCRL, SYNCRL, KTCRL)
    /// </summary>
    public class CrossRefCommands
    {
        /// <summary>
        /// TCCTB : Tham chiếu chân thiết bị (Liên kết cuộn hút Coil với các tiếp điểm NO/NC)
        /// </summary>
        [CommandMethod("TCCTB")]
        public void CrossRefLink()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n--- THAM CHIEU CHAN TIEP DIEM THIET BI (TCCTB) ---");

            // 1. Chon cuon hut (Coil / Master Block)
            var peoCoil = new PromptEntityOptions("\nChon cuon hut (Coil/Master Block) cua thiet bi: ");
            peoCoil.SetRejectMessage("\nDoi tuong phai la Block Reference.");
            peoCoil.AddAllowedClass(typeof(BlockReference), true);
            var perCoil = ed.GetEntity(peoCoil);
            if (perCoil.Status != PromptStatus.OK) return;

            string deviceTag = "";
            Point3d coilPos = Point3d.Origin;

            CadDatabaseHelper.RunTransaction((tr, db) =>
            {
                var coilRef = (BlockReference)tr.GetObject(perCoil.ObjectId, OpenMode.ForRead);
                coilPos = coilRef.Position;
                deviceTag = coilRef.GetAttributeValue(tr, ElectricalConfig.TagDeviceName, "TAG", "NAME", "DEVICE") ?? "KA1";
            });

            ed.WriteMessage($"\nDa chon thiet bi: '{deviceTag}'. Bay gio hay click chon cac tiep diem (Contact) de lien ket:");

            // 2. Chon cac tiep diem lien ket
            int linkedContacts = 0;
            while (true)
            {
                var peoContact = new PromptEntityOptions($"\nChon tiep diem (NO/NC) de gan cho '{deviceTag}' (ESC de ket thuc): ");
                peoContact.SetRejectMessage("\nDoi tuong phai la Block Reference.");
                peoContact.AddAllowedClass(typeof(BlockReference), true);
                var perContact = ed.GetEntity(peoContact);
                if (perContact.Status != PromptStatus.OK) break;

                CadDatabaseHelper.RunTransaction((tr, db) =>
                {
                    var contactRef = (BlockReference)tr.GetObject(perContact.ObjectId, OpenMode.ForWrite);

                    // Gan ten thiet bi (NAME) vao tiep diem
                    contactRef.SetAttributeValue(ElectricalConfig.TagDeviceName, deviceTag, tr);

                    // Lay so chan hien tai (TERM01, TERM02)
                    string p1 = contactRef.GetAttributeValue(tr, ElectricalConfig.TagPin1, "TERM01", "PIN1") ?? "";
                    string p2 = contactRef.GetAttributeValue(tr, ElectricalConfig.TagPin2, "TERM02", "PIN2") ?? "";

                    linkedContacts++;
                    ed.WriteMessage($" -> Da gan: {deviceTag} [Chan: {p1}-{p2}]");
                });
            }

            ed.WriteMessage($"\n[TCCTB] Hoan tat lien ket {linkedContacts} tiep diem cho thiet bi '{deviceTag}'.");
        }

        /// <summary>
        /// TKCRL : Thống kê chân số thiết bị & điền vào block KHUNG 14 CHÂN
        ///
        /// QUY TRÌNH SỬ DỤNG:
        ///  1. Gõ lệnh TKCRL
        ///  2. Chọn block cuộn hút hoặc block KHUNG 14 CHÂN của relay cần thống kê
        ///  3. Quét chọn các khung tên (Frame-a4) để xác định phạm vi trang
        ///  4. Plugin tự động:
        ///     a. Tìm tên thiết bị (NAME) từ block đã chọn
        ///     b. Quét tất cả block tiếp điểm có cùng NAME trên toàn bộ bản vẽ
        ///     c. Xác định mỗi tiếp điểm nằm trong khung tên nào → lấy số trang
        ///     d. Tính toán vị trí hàng-cột (ví dụ: 6B) trong khung đó
        ///     e. Ghi kết quả vào ADDRESS_1 ~ ADDRESS_4 trên block KHUNG 14 CHÂN
        /// </summary>
        [CommandMethod("TKCRL")]
        public void SummaryRelayPins()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n=======================================================");
            ed.WriteMessage("\n THONG KE CHAN SO THIET BI & DIEN KHUNG 14 CHAN (TKCRL)");
            ed.WriteMessage("\n=======================================================");

            // ──────────── BUOC 1: Chon block thiet bi (Coil hoac KHUNG 14 CHAN) ────────────
            var peoDevice = new PromptEntityOptions("\nBuoc 1: Chon block CUON HUT hoac KHUNG 14 CHAN cua thiet bi can thong ke: ");
            peoDevice.SetRejectMessage("\nDoi tuong phai la Block Reference.");
            peoDevice.AddAllowedClass(typeof(BlockReference), true);
            var perDevice = ed.GetEntity(peoDevice);
            if (perDevice.Status != PromptStatus.OK) return;

            string deviceName = "";
            ObjectId relayFrameId = ObjectId.Null;

            CadDatabaseHelper.RunTransaction((tr, db) =>
            {
                var blkRef = (BlockReference)tr.GetObject(perDevice.ObjectId, OpenMode.ForRead);
                string blkName = blkRef.GetEffectiveBlockName(tr);

                // Lay ten thiet bi
                deviceName = blkRef.GetAttributeValue(tr, ElectricalConfig.TagDeviceName, ElectricalConfig.TagRelayFrameName, "TAG", "NAME") ?? "";

                // Kiem tra neu day la block KHUNG 14 CHAN thi ghi nho ObjectId de sau ghi ket qua
                if (string.Equals(blkName, ElectricalConfig.BlockRelayFrame, StringComparison.OrdinalIgnoreCase))
                {
                    relayFrameId = blkRef.ObjectId;
                }
            });

            if (string.IsNullOrWhiteSpace(deviceName))
            {
                ed.WriteMessage("\n[TKCRL] Khong doc duoc ten thiet bi (Tag 'NAME'). Vui long chon block co Attribute NAME.");
                return;
            }

            ed.WriteMessage($"\n -> Ten thiet bi: '{deviceName}'");

            // ──────────── BUOC 2: Quet chon cac khung ten (Frame) ────────────
            ed.WriteMessage("\nBuoc 2: Quet chon TAT CA cac khung ten (Frame) tren ban ve:");

            var filter = SelectionHelper.CreateTypeFilter("INSERT");
            var selRes = ed.GetSelection(new PromptSelectionOptions
            {
                MessageForAdding = "\nQuet chon cac khung ten (Frame-a4): "
            }, filter);

            if (selRes.Status != PromptStatus.OK || selRes.Value == null || selRes.Value.Count == 0)
            {
                ed.WriteMessage("\nChua chon khung ten nao. Huy lenh.");
                return;
            }

            // ──────────── BUOC 3: Xu ly chinh ────────────
            // Bien tam luu ket qua de dung ngoai transaction (cho PromptPoint)
            List<ContactDetail> contactResults = null;
            List<SheetFrame> frameResults = null;
            bool needInsertNewFrame = false;

            CadDatabaseHelper.RunTransaction((tr, db) =>
            {
                // 3a. Quet cac khung ten tu doi tuong da chon
                var selectedIds = selRes.Value.Cast<SelectedObject>().Select(x => x.ObjectId);
                var sheetNoTags = new[] { ElectricalConfig.TagSheetNumber, "A00", "SHEET_NO", "SHEET", "PAGE" };
                var frames = SheetFrameHelper.ScanSelectedFrames(tr, selectedIds, sheetNoTags);

                if (frames.Count == 0)
                {
                    ed.WriteMessage($"\n[TKCRL] Khong tim thay khung ten nao co Tag so trang ({string.Join("/", sheetNoTags)}) trong cac block da chon.");
                    return;
                }

                ed.WriteMessage($"\n -> Tim thay {frames.Count} khung ten: Trang {string.Join(", ", frames.Select(f => f.SheetNo))}");
                frameResults = frames;

                // 3b. Quet tat ca block tiep diem co cung NAME tren toan ban ve
                var contacts = new List<ContactDetail>();
                var allBlocks = SelectionHelper.GetAllEntitiesOfType<BlockReference>(db, tr);

                foreach (var id in allBlocks)
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is BlockReference blkRef)
                    {
                        string? name = blkRef.GetAttributeValue(tr, ElectricalConfig.TagDeviceName, "TAG", "NAME");
                        if (!string.Equals(name, deviceName, StringComparison.OrdinalIgnoreCase)) continue;

                        string blkName = blkRef.GetEffectiveBlockName(tr);
                        // Bo qua chinh block KHUNG 14 CHAN
                        if (string.Equals(blkName, ElectricalConfig.BlockRelayFrame, StringComparison.OrdinalIgnoreCase)) continue;

                        string p1 = blkRef.GetAttributeValue(tr, ElectricalConfig.TagPin1, "TERM01", "PIN1") ?? "";
                        string p2 = blkRef.GetAttributeValue(tr, ElectricalConfig.TagPin2, "TERM02", "PIN2") ?? "";
                        string pinPair = string.IsNullOrWhiteSpace(p2) ? p1 : $"{p1}-{p2}";

                        // Xac dinh trang va vi tri
                        var frame = SheetFrameHelper.FindFrameContaining(frames, blkRef.Position);
                        string address = "-";
                        if (frame != null)
                        {
                            address = frame.GetAddress(
                                blkRef.Position,
                                ElectricalConfig.FrameRows,
                                ElectricalConfig.FrameColumns,
                                ElectricalConfig.ColumnLetters);
                        }

                        contacts.Add(new ContactDetail
                        {
                            PinPair = pinPair,
                            Address = address,
                            Position = blkRef.Position,
                            BlockName = blkName
                        });
                    }
                }

                if (contacts.Count == 0)
                {
                    ed.WriteMessage($"\n[TKCRL] Khong tim thay tiep diem nao co NAME = '{deviceName}' tren ban ve.");
                    return;
                }

                contactResults = contacts;

                // 3c. In ket qua ra dong lenh
                ed.WriteMessage($"\n -> Tim thay {contacts.Count} tiep diem cua '{deviceName}':");
                foreach (var c in contacts)
                {
                    ed.WriteMessage($"\n   Chan [{c.PinPair,-8}] -> Dia chi: {c.Address,-8} (Block: {c.BlockName})");
                }

                // 3d. Tim block KHUNG 14 CHAN da co san tren ban ve
                BlockReference? relayFrame = null;

                if (relayFrameId != ObjectId.Null)
                {
                    relayFrame = tr.GetObject(relayFrameId, OpenMode.ForWrite) as BlockReference;
                }
                else
                {
                    // Tim tu dong: Quet toan ban ve tim block KHUNG 14 CHAN co cung NAME
                    foreach (var id in allBlocks)
                    {
                        if (tr.GetObject(id, OpenMode.ForRead) is BlockReference blkRef)
                        {
                            string blkName = blkRef.GetEffectiveBlockName(tr);
                            if (!string.Equals(blkName, ElectricalConfig.BlockRelayFrame, StringComparison.OrdinalIgnoreCase)) continue;

                            string? frameName = blkRef.GetAttributeValue(tr, ElectricalConfig.TagRelayFrameName, "NAME");
                            if (string.Equals(frameName, deviceName, StringComparison.OrdinalIgnoreCase))
                            {
                                relayFrame = (BlockReference)tr.GetObject(id, OpenMode.ForWrite);
                                break;
                            }
                        }
                    }
                }

                if (relayFrame != null)
                {
                    // Da tim thay block KHUNG 14 CHAN → ghi dia chi truc tiep
                    FillRelayFrameAddresses(relayFrame, contacts, deviceName, ed, tr);
                }
                else
                {
                    // Khong tim thay → danh dau de tao moi o buoc tiep theo
                    needInsertNewFrame = true;
                    ed.WriteMessage($"\n\n -> Khong tim thay block '{ElectricalConfig.BlockRelayFrame}' cho '{deviceName}'. Se tu dong tao moi!");
                }
            });

            // ──────────── BUOC 4: Tu dong tao block KHUNG 14 CHAN (neu chua co) ────────────
            if (needInsertNewFrame && contactResults != null && contactResults.Count > 0)
            {
                ed.WriteMessage($"\nBuoc 3: Chon diem dat block KHUNG 14 CHAN moi cho '{deviceName}':");

                var ppo = new PromptPointOptions($"\nChon diem dat KHUNG 14 CHAN cho '{deviceName}': ");
                var ppr = ed.GetPoint(ppo);

                if (ppr.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n[TKCRL] Da huy. Ket qua thong ke da duoc in ra dong lenh o tren.");
                    return;
                }

                Point3d insertPoint = ppr.Value;

                CadDatabaseHelper.RunTransaction((tr, db) =>
                {
                    // Kiem tra block definition KHUNG 14 CHAN co ton tai trong ban ve khong
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    if (!bt.Has(ElectricalConfig.BlockRelayFrame))
                    {
                        ed.WriteMessage($"\n[LOI] Block Definition '{ElectricalConfig.BlockRelayFrame}' chua ton tai trong ban ve!");
                        ed.WriteMessage($"\n      Vui long tao block '{ElectricalConfig.BlockRelayFrame}' truoc (ve khung 14 chan roi luu thanh block).");
                        ed.WriteMessage($"\n      Hoac dung lenh TAOBLOCKMAU de tao cac block mau.");
                        return;
                    }

                    // Chuan bi du lieu Attribute cho block moi
                    var attrValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        { ElectricalConfig.TagRelayFrameName, deviceName },
                        { ElectricalConfig.TagRelayFrameModel, "" },
                        { ElectricalConfig.TagRelayFrameCoil, "" },
                    };

                    // Reset tat ca ADDRESS ve "-" truoc
                    foreach (var addrTag in ElectricalConfig.TagRelayFrameAddresses)
                    {
                        attrValues[addrTag] = "-";
                    }

                    // Tinh toan va dien dia chi tu contacts
                    foreach (var contact in contactResults)
                    {
                        int addrIndex = ResolveAddressIndex(contact.PinPair);

                        if (addrIndex >= 0 && addrIndex < ElectricalConfig.TagRelayFrameAddresses.Length)
                        {
                            string addrTag = ElectricalConfig.TagRelayFrameAddresses[addrIndex];
                            string currentVal = attrValues.ContainsKey(addrTag) ? attrValues[addrTag] : "-";

                            if (currentVal != "-" && !string.IsNullOrWhiteSpace(currentVal))
                            {
                                attrValues[addrTag] = currentVal + "," + contact.Address;
                            }
                            else
                            {
                                attrValues[addrTag] = contact.Address;
                            }
                        }
                    }

                    // Insert block KHUNG 14 CHAN moi vao ban ve
                    try
                    {
                        var newBlockId = BlockDefinitionHelper.InsertBlock(
                            db, tr,
                            ElectricalConfig.BlockRelayFrame,
                            insertPoint,
                            attrValues);

                        int filledCount = contactResults.Count(c => ResolveAddressIndex(c.PinPair) >= 0);
                        ed.WriteMessage($"\n\n[TKCRL] HOAN TAT!");
                        ed.WriteMessage($"\n   -> Da TAO MOI block '{ElectricalConfig.BlockRelayFrame}' tai ({insertPoint.X:F0}, {insertPoint.Y:F0})");
                        ed.WriteMessage($"\n   -> NAME = '{deviceName}'");
                        ed.WriteMessage($"\n   -> Da dien {filledCount} dia chi tham chieu (ADDRESS_1~4)");
                        ed.WriteMessage($"\n   -> Cac ADDRESS tren khung 14 chan da duoc cap nhat thanh cong!");
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\n[LOI] Khong the chen block: {ex.Message}");
                    }
                });
            }
        }

        /// <summary>
        /// SYNCRL : Cập nhật lại toàn bộ địa chỉ ADDRESS trên tất cả block KHUNG 14 CHÂN tự động
        /// </summary>
        [CommandMethod("SYNCRL")]
        public void SyncRelayPins()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n--- DONG BO TOAN BO DIA CHI CHAN SO THIET BI (SYNCRL) ---");

            // Quet chon khung ten
            var filter = SelectionHelper.CreateTypeFilter("INSERT");
            var selRes = ed.GetSelection(new PromptSelectionOptions
            {
                MessageForAdding = "\nQuet chon TAT CA cac khung ten (Frame) de xac dinh trang: "
            }, filter);

            if (selRes.Status != PromptStatus.OK || selRes.Value == null || selRes.Value.Count == 0)
            {
                ed.WriteMessage("\nChua chon khung ten nao. Huy lenh.");
                return;
            }

            int totalRelays = 0;
            int totalContacts = 0;

            CadDatabaseHelper.RunTransaction((tr, db) =>
            {
                var selectedIds = selRes.Value.Cast<SelectedObject>().Select(x => x.ObjectId);
                var sheetNoTags = new[] { ElectricalConfig.TagSheetNumber, "A00", "SHEET_NO", "SHEET", "PAGE" };
                var frames = SheetFrameHelper.ScanSelectedFrames(tr, selectedIds, sheetNoTags);

                if (frames.Count == 0)
                {
                    ed.WriteMessage("\n[SYNCRL] Khong tim thay khung ten nao co Tag so trang.");
                    return;
                }

                ed.WriteMessage($"\n -> {frames.Count} khung ten da nhan dien.");

                // Tim tat ca block KHUNG 14 CHAN
                var allBlocks = SelectionHelper.GetAllEntitiesOfType<BlockReference>(db, tr);
                var relayFrames = new List<(BlockReference frame, string name)>();

                foreach (var id in allBlocks)
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is BlockReference blkRef)
                    {
                        string blkName = blkRef.GetEffectiveBlockName(tr);
                        if (string.Equals(blkName, ElectricalConfig.BlockRelayFrame, StringComparison.OrdinalIgnoreCase))
                        {
                            string? name = blkRef.GetAttributeValue(tr, ElectricalConfig.TagRelayFrameName, "NAME");
                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                relayFrames.Add((blkRef, name!));
                            }
                        }
                    }
                }

                if (relayFrames.Count == 0)
                {
                    ed.WriteMessage($"\n[SYNCRL] Khong tim thay block '{ElectricalConfig.BlockRelayFrame}' nao tren ban ve.");
                    return;
                }

                ed.WriteMessage($"\n -> Tim thay {relayFrames.Count} block khung 14 chan.");

                // Voi moi relay frame, quet tiep diem va cap nhat dia chi
                foreach (var (relayFrame, relayName) in relayFrames)
                {
                    var rfWrite = (BlockReference)tr.GetObject(relayFrame.ObjectId, OpenMode.ForWrite);

                    // Reset ADDRESS
                    foreach (var addrTag in ElectricalConfig.TagRelayFrameAddresses)
                    {
                        rfWrite.SetAttributeValue(addrTag, "-", tr);
                    }

                    // Tim cac tiep diem cung NAME
                    foreach (var id in allBlocks)
                    {
                        if (tr.GetObject(id, OpenMode.ForRead) is BlockReference blkRef)
                        {
                            string blkName = blkRef.GetEffectiveBlockName(tr);
                            if (string.Equals(blkName, ElectricalConfig.BlockRelayFrame, StringComparison.OrdinalIgnoreCase)) continue;

                            string? name = blkRef.GetAttributeValue(tr, ElectricalConfig.TagDeviceName, "TAG", "NAME");
                            if (!string.Equals(name, relayName, StringComparison.OrdinalIgnoreCase)) continue;

                            string p1 = blkRef.GetAttributeValue(tr, ElectricalConfig.TagPin1, "TERM01") ?? "";
                            string p2 = blkRef.GetAttributeValue(tr, ElectricalConfig.TagPin2, "TERM02") ?? "";
                            string pinPair = string.IsNullOrWhiteSpace(p2) ? p1 : $"{p1}-{p2}";

                            var frame = SheetFrameHelper.FindFrameContaining(frames, blkRef.Position);
                            if (frame == null) continue;

                            string address = frame.GetAddress(blkRef.Position, ElectricalConfig.FrameRows, ElectricalConfig.FrameColumns, ElectricalConfig.ColumnLetters);

                            var normalized = NormalizePinPair(pinPair) ?? pinPair;
                            if (ElectricalConfig.PinPairToAddressIndex.TryGetValue(normalized, out int addrIdx) &&
                                addrIdx >= 0 && addrIdx < ElectricalConfig.TagRelayFrameAddresses.Length)
                            {
                                string addrTag = ElectricalConfig.TagRelayFrameAddresses[addrIdx];
                                string currentVal = rfWrite.GetAttributeValue(addrTag, tr) ?? "-";

                                if (currentVal != "-" && !string.IsNullOrWhiteSpace(currentVal))
                                    rfWrite.SetAttributeValue(addrTag, currentVal + "," + address, tr);
                                else
                                    rfWrite.SetAttributeValue(addrTag, address, tr);

                                totalContacts++;
                            }
                        }
                    }

                    totalRelays++;
                    ed.WriteMessage($"\n   Relay '{relayName}': Da cap nhat xong.");
                }
            });

            ed.WriteMessage($"\n\n[SYNCRL] HOAN TAT! Da dong bo {totalContacts} dia chi tham chieu cho {totalRelays} thiet bi!");
        }

        /// <summary>
        /// KTCRL : Kiểm tra chân số thiết bị (Validation Engine - Cảnh báo trùng chân)
        /// </summary>
        [CommandMethod("KTCRL")]
        public void ValidateRelayPins()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n--- KIEM TRA CHAN SO THIET BI (KTCRL) ---");

            var contactsByDevice = new Dictionary<string, List<(string Pins, Point3d Pos)>>(StringComparer.OrdinalIgnoreCase);

            CadDatabaseHelper.RunTransaction((tr, db) =>
            {
                var blockIds = SelectionHelper.GetAllEntitiesOfType<BlockReference>(db, tr);
                foreach (var id in blockIds)
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is BlockReference blkRef)
                    {
                        string blkName = blkRef.GetEffectiveBlockName(tr);
                        if (string.Equals(blkName, ElectricalConfig.BlockRelayFrame, StringComparison.OrdinalIgnoreCase)) continue;

                        string? devName = blkRef.GetAttributeValue(tr, ElectricalConfig.TagDeviceName, "TAG", "NAME");
                        if (!string.IsNullOrWhiteSpace(devName))
                        {
                            string p1 = blkRef.GetAttributeValue(tr, ElectricalConfig.TagPin1, "TERM01", "PIN1") ?? "";
                            string p2 = blkRef.GetAttributeValue(tr, ElectricalConfig.TagPin2, "TERM02", "PIN2") ?? "";
                            string pin = string.IsNullOrWhiteSpace(p2) ? p1 : $"{p1}-{p2}";

                            if (!string.IsNullOrWhiteSpace(pin))
                            {
                                if (!contactsByDevice.ContainsKey(devName!))
                                {
                                    contactsByDevice[devName!] = new List<(string Pins, Point3d Pos)>();
                                }
                                contactsByDevice[devName!].Add((pin, blkRef.Position));
                            }
                        }
                    }
                }
            });

            int errorCount = 0;
            foreach (var kvp in contactsByDevice)
            {
                var pinGroups = kvp.Value.GroupBy(x => NormalizePinPair(x.Pins) ?? x.Pins, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1);
                foreach (var duplicate in pinGroups)
                {
                    errorCount++;
                    ed.WriteMessage($"\n [CANH BAO] Thiet bi '{kvp.Key}' bi TRUNG CHAN: Cap chan [{duplicate.Key}] xuat hien {duplicate.Count()} lan!");
                    foreach (var pos in duplicate)
                    {
                        ed.WriteMessage($"\n            -> Vi tri: ({pos.Pos.X:F0}, {pos.Pos.Y:F0})");
                    }
                }
            }

            if (errorCount == 0)
            {
                ed.WriteMessage($"\n[KTCRL] KET QUA: Tat ca {contactsByDevice.Values.Sum(v => v.Count)} chan so cua {contactsByDevice.Count} thiet bi deu hop le! (OK)");
            }
            else
            {
                ed.WriteMessage($"\n[KTCRL] Phat hien tong cong {errorCount} loi trung chan so! Vui long kiem tra lai.");
            }
        }

        /// <summary>
        /// Chuẩn hóa cặp chân "5-9" hoặc "9-5" thành "5-9" (số nhỏ trước)
        /// </summary>
        private static string? NormalizePinPair(string pinPair)
        {
            if (string.IsNullOrWhiteSpace(pinPair)) return null;

            var parts = pinPair.Split('-');
            if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out int a) && int.TryParse(parts[1].Trim(), out int b))
            {
                return a <= b ? $"{a}-{b}" : $"{b}-{a}";
            }
            return pinPair;
        }

        private class ContactDetail
        {
            public string PinPair { get; set; } = string.Empty;
            public string Address { get; set; } = string.Empty;
            public Point3d Position { get; set; }
            public string BlockName { get; set; } = string.Empty;
        }

        /// <summary>
        /// Ghi địa chỉ ADDRESS_1~4 vào block KHUNG 14 CHÂN đã tồn tại
        /// </summary>
        private void FillRelayFrameAddresses(
            BlockReference relayFrame,
            List<ContactDetail> contacts,
            string deviceName,
            Autodesk.AutoCAD.EditorInput.Editor ed,
            Transaction tr)
        {
            // Reset tat ca ADDRESS ve "-" truoc
            foreach (var addrTag in ElectricalConfig.TagRelayFrameAddresses)
            {
                relayFrame.SetAttributeValue(addrTag, "-", tr);
            }

            int filledCount = 0;
            foreach (var contact in contacts)
            {
                int addrIndex = ResolveAddressIndex(contact.PinPair);

                if (addrIndex >= 0 && addrIndex < ElectricalConfig.TagRelayFrameAddresses.Length)
                {
                    string addrTag = ElectricalConfig.TagRelayFrameAddresses[addrIndex];
                    string currentVal = relayFrame.GetAttributeValue(addrTag, tr) ?? "-";

                    if (currentVal != "-" && !string.IsNullOrWhiteSpace(currentVal))
                    {
                        relayFrame.SetAttributeValue(addrTag, currentVal + "," + contact.Address, tr);
                    }
                    else
                    {
                        relayFrame.SetAttributeValue(addrTag, contact.Address, tr);
                    }
                    filledCount++;
                }
                else
                {
                    ed.WriteMessage($"\n   [CANH BAO] Cap chan [{contact.PinPair}] khong khop voi bang PinPairToAddressIndex trong ElectricalConfig.");
                }
            }

            ed.WriteMessage($"\n\n[TKCRL] HOAN TAT! Da dien {filledCount} dia chi tham chieu vao block '{ElectricalConfig.BlockRelayFrame}' cua '{deviceName}'.");
            ed.WriteMessage("\n        Cac ADDRESS tren khung 14 chan da duoc cap nhat thanh cong!");
        }

        /// <summary>
        /// Tìm index ADDRESS (0~3) từ cặp chân (ví dụ "5-9" → 0)
        /// </summary>
        private static int ResolveAddressIndex(string pinPair)
        {
            if (ElectricalConfig.PinPairToAddressIndex.TryGetValue(pinPair, out int idx))
            {
                return idx;
            }

            var pinNormalized = NormalizePinPair(pinPair);
            if (pinNormalized != null && ElectricalConfig.PinPairToAddressIndex.TryGetValue(pinNormalized, out int idx2))
            {
                return idx2;
            }

            return -1;
        }
    }
}
