using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using CadElectricalToolkit.CadAccess;
using CadElectricalToolkit.Core;
using CadElectricalToolkit.UI.Views;

namespace CadElectricalToolkit.Commands
{
    /// <summary>
    /// Các lệnh tham chiếu tiếp điểm & cuộn hút Rơ-le/Contactor/Timer (TCCTB, TKCRL, SYNCRL, KTCRL)
    /// </summary>
    public class CrossRefCommands
    {
        /// <summary>
        /// TCCTB : Tham chiếu chân thiết bị (Đánh lại địa chỉ cuộn coil cho tiếp điểm với số lượng lớn)
        /// </summary>
        [CommandMethod("TCCTB")]
        public void CrossRefLink()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n==================================================================");
            ed.WriteMessage("\n  DANH LAI DIA CHI CUON COIL CHO TIEP DIEM SO LUONG LON (TCCTB)");
            ed.WriteMessage("\n==================================================================");

            // Cho phep quet chon hoac nhan ENTER de tu dong quet toan bo
            var pso = new PromptSelectionOptions
            {
                MessageForAdding = "\nQuet chon cac block Cuon coil & Tiep diem (nhan ENTER de tu dong xu ly TOAN BO ban ve): "
            };
            var filter = SelectionHelper.CreateTypeFilter("INSERT");
            var selRes = ed.GetSelection(pso, filter);

            if (selRes.Status == PromptStatus.Cancel)
            {
                ed.WriteMessage("\nDa huy lenh TCCTB.");
                return;
            }

            bool processAll = (selRes.Status != PromptStatus.OK || selRes.Value == null || selRes.Value.Count == 0);

            if (processAll)
            {
                ed.WriteMessage("\n -> Che do: Tu dong quet va cap nhat TOAN BO ban ve.");
            }
            else
            {
                ed.WriteMessage($"\n -> Che do: Xu ly {selRes.Value?.Count ?? 0} block duoc chon.");
            }

            CadDatabaseHelper.RunTransaction((tr, db) =>
            {
                var frames = SheetFrameHelper.ScanAllFramesAuto(db, tr);
                if (frames.Count == 0)
                {
                    ed.WriteMessage("\n[CANH BAO] Khong tim thay khung ten nao co Tag so trang (A00/SHEET_NO) tren ban ve.");
                    return;
                }

                ed.WriteMessage($"\n -> Da nhan dien {frames.Count} khung ten: Trang {string.Join(", ", frames.Select(f => f.SheetNo))}");

                IEnumerable<ObjectId>? targetIds = (processAll || selRes.Value == null)
                    ? null
                    : selRes.Value.Cast<SelectedObject>().Select(s => s.ObjectId);

                var (coilsFound, contactsUpdated, details) = SyncCoilAddressesToContacts(tr, db, frames, targetIds);

                ed.WriteMessage("\n--------------------------------------------------------------");
                ed.WriteMessage("\n [TCCTB] KET QUA DANH DIA CHI CUON COIL HANG LOAT:");
                foreach (var kvp in details)
                {
                    ed.WriteMessage($"\n  * Thiet bi '{kvp.Key}': Da cap nhat {kvp.Value.Count} tiep diem: [{string.Join(", ", kvp.Value)}]");
                }
                ed.WriteMessage("\n--------------------------------------------------------------");
                ed.WriteMessage($"\n[TCCTB] HOAN TAT! Da danh lai dia chi cuon coil cho {contactsUpdated} tiep diem cua {details.Count} thiet bi!\n");
            });
        }

        /// <summary>
        /// TKCRL : Thống kê chân số thiết bị & điền vào block KHUNG 14 CHÂN
        ///
        /// QUY TRÌNH SỬ DỤNG:
        ///  1. Gõ lệnh TKCRL
        ///  2. Quét chọn NHIỀU block cuộn hút (Coil) hoặc KHUNG 14 CHÂN cùng lúc
        ///  3. Quét chọn các khung tên (Frame-a4) để xác định phạm vi trang
        ///  4. Plugin tự động xử lý TỪNG relay:
        ///     a. Tìm tên thiết bị (NAME) từ block đã chọn
        ///     b. Quét tất cả block tiếp điểm có cùng NAME trên toàn bộ bản vẽ
        ///     c. Xác định mỗi tiếp điểm nằm trong khung tên nào → lấy số trang
        ///     d. Tính toán vị trí hàng-cột (ví dụ: 6B) trong khung đó
        ///     e. Nếu đã có KHUNG 14 CHÂN → ghi trực tiếp
        ///        Nếu chưa có → tự tạo mới, xếp cạnh nhau tại vị trí người dùng chọn
        /// </summary>
        [CommandMethod("TKCRL")]
        public void SummaryRelayPins()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n=======================================================");
            ed.WriteMessage("\n THONG KE CHAN SO THIET BI & DIEN KHUNG 14 CHAN (TKCRL)");
            ed.WriteMessage("\n=======================================================");

            // ──────────── BUOC 1: Quet chon NHIEU cuon coil hoac KHUNG 14 CHAN ────────────
            ed.WriteMessage("\nBuoc 1: Quet chon cac block CUON HUT (Coil) can thong ke:");

            var filterCoils = SelectionHelper.CreateTypeFilter("INSERT");
            var selCoils = ed.GetSelection(new PromptSelectionOptions
            {
                MessageForAdding = "\nQuet chon cac cuon hut relay (co the chon nhieu): "
            }, filterCoils);

            if (selCoils.Status != PromptStatus.OK || selCoils.Value == null || selCoils.Value.Count == 0)
            {
                ed.WriteMessage("\nChua chon cuon hut nao. Huy lenh.");
                return;
            }

            // Doc ten thiet bi tu cac block da chon
            var deviceInfos = new List<DeviceInfo>(); // Luu thong tin cac relay da chon

            CadDatabaseHelper.RunTransaction((tr, db) =>
            {
                foreach (SelectedObject selObj in selCoils.Value)
                {
                    if (selObj == null) continue;
                    if (tr.GetObject(selObj.ObjectId, OpenMode.ForRead) is BlockReference blkRef)
                    {
                        string? name = blkRef.GetAttributeValue(tr, ElectricalConfig.TagDeviceName, ElectricalConfig.TagRelayFrameName, "TAG", "NAME");
                        if (string.IsNullOrWhiteSpace(name)) continue;

                        string blkName = blkRef.GetEffectiveBlockName(tr);
                        bool isRelayFrame = string.Equals(blkName, ElectricalConfig.BlockRelayFrame, StringComparison.OrdinalIgnoreCase);

                        // Tranh trung ten thiet bi
                        if (deviceInfos.Any(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase))) continue;

                        deviceInfos.Add(new DeviceInfo
                        {
                            Name = name!,
                            ExistingFrameId = isRelayFrame ? blkRef.ObjectId : ObjectId.Null,
                            CoilBlockId = isRelayFrame ? ObjectId.Null : blkRef.ObjectId,
                            CoilPosition = blkRef.Position
                        });
                    }
                }
            });

            if (deviceInfos.Count == 0)
            {
                ed.WriteMessage("\n[TKCRL] Khong doc duoc ten thiet bi (Tag 'NAME') tu cac block da chon.");
                return;
            }

            ed.WriteMessage($"\n -> Da chon {deviceInfos.Count} thiet bi: {string.Join(", ", deviceInfos.Select(d => d.Name))}");

            // ──────────── BUOC 2: Quet chon cac khung ten (Frame) ────────────
            ed.WriteMessage("\nBuoc 2: Quet chon TAT CA cac khung ten (Frame) tren ban ve:");

            var filterFrame = SelectionHelper.CreateTypeFilter("INSERT");
            var selFrames = ed.GetSelection(new PromptSelectionOptions
            {
                MessageForAdding = "\nQuet chon cac khung ten (Frame-a4): "
            }, filterFrame);

            if (selFrames.Status != PromptStatus.OK || selFrames.Value == null || selFrames.Value.Count == 0)
            {
                ed.WriteMessage("\nChua chon khung ten nao. Huy lenh.");
                return;
            }

            // ──────────── BUOC 3: Quet tiep diem va phan loai ────────────
            // Luu ket qua: deviceName -> danh sach tiep diem + dia chi
            var deviceContacts = new Dictionary<string, List<ContactDetail>>(StringComparer.OrdinalIgnoreCase);
            var devicesNeedNewFrame = new List<string>(); // Cac thiet bi can tao KHUNG 14 CHAN moi

            CadDatabaseHelper.RunTransaction((tr, db) =>
            {
                // 3a. Quet khung ten
                var selectedFrameIds = selFrames.Value.Cast<SelectedObject>().Select(x => x.ObjectId);
                var sheetNoTags = new[] { ElectricalConfig.TagSheetNumber, "A00", "SHEET_NO", "SHEET", "PAGE" };
                var frames = SheetFrameHelper.ScanSelectedFrames(tr, selectedFrameIds, sheetNoTags);

                if (frames.Count == 0)
                {
                    frames = SheetFrameHelper.ScanAllFramesAuto(db, tr);
                }

                if (frames.Count == 0)
                {
                    ed.WriteMessage($"\n[TKCRL] Khong tim thay khung ten nao co Tag so trang ({string.Join("/", sheetNoTags)}).");
                    return;
                }

                ed.WriteMessage($"\n -> Tim thay {frames.Count} khung ten: Trang {string.Join(", ", frames.Select(f => f.SheetNo))}");

                // 3b. Dong bo dia chi cuon coil vao tag ADDRESS_COIL cua cac tiep diem
                var (coilsFound, contactsUpdated, coilDetails) = SyncCoilAddressesToContacts(
                    tr, db, frames, null, deviceInfos);

                if (contactsUpdated > 0)
                {
                    ed.WriteMessage($"\n -> Da dong bo ADDRESS_COIL cho {contactsUpdated} tiep diem thuoc {deviceInfos.Count} thiet bi.");
                }

                // 3c. Quet tat ca block tren ban ve 1 lan duy nhat
                var allBlocks = SelectionHelper.GetAllEntitiesOfType<BlockReference>(db, tr);

                // Xay dung Map: deviceName -> danh sach tiep diem
                foreach (var id in allBlocks)
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is BlockReference blkRef)
                    {
                        string blkName = blkRef.GetEffectiveBlockName(tr);
                        if (string.Equals(blkName, ElectricalConfig.BlockRelayFrame, StringComparison.OrdinalIgnoreCase)) continue;

                        string? name = blkRef.GetAttributeValue(tr, ElectricalConfig.TagDeviceName, "TAG", "NAME");
                        if (string.IsNullOrWhiteSpace(name)) continue;

                        // Chi xu ly cac thiet bi da chon o buoc 1
                        if (!deviceInfos.Any(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase))) continue;

                        string p1 = blkRef.GetAttributeValue(tr, ElectricalConfig.TagPin1, "TERM01", "PIN1") ?? "";
                        string p2 = blkRef.GetAttributeValue(tr, ElectricalConfig.TagPin2, "TERM02", "PIN2") ?? "";
                        string pinPair = string.IsNullOrWhiteSpace(p2) ? p1 : $"{p1}-{p2}";

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

                        if (!deviceContacts.ContainsKey(name!))
                        {
                            deviceContacts[name!] = new List<ContactDetail>();
                        }

                        deviceContacts[name!].Add(new ContactDetail
                        {
                            PinPair = pinPair,
                            Address = address,
                            Position = blkRef.Position,
                            BlockName = blkName
                        });
                    }
                }

                // 3c. Voi moi thiet bi, kiem tra da co KHUNG 14 CHAN chua
                foreach (var dev in deviceInfos)
                {
                    if (!deviceContacts.ContainsKey(dev.Name) || deviceContacts[dev.Name].Count == 0)
                    {
                        ed.WriteMessage($"\n   [CANH BAO] '{dev.Name}': Khong tim thay tiep diem nao.");
                        continue;
                    }

                    var contacts = deviceContacts[dev.Name];
                    ed.WriteMessage($"\n\n   [{dev.Name}] Tim thay {contacts.Count} tiep diem:");
                    foreach (var c in contacts)
                    {
                        ed.WriteMessage($"\n     Chan [{c.PinPair,-8}] -> {c.Address,-8} (Block: {c.BlockName})");
                    }

                    // Tim block KHUNG 14 CHAN da co tren ban ve
                    BlockReference? existingFrame = null;

                    if (dev.ExistingFrameId != ObjectId.Null)
                    {
                        existingFrame = tr.GetObject(dev.ExistingFrameId, OpenMode.ForRead) as BlockReference;
                        if (existingFrame != null && !existingFrame.IsWriteEnabled)
                        {
                            existingFrame.UpgradeOpen();
                        }
                    }
                    else
                    {
                        // Tim tu dong tren toan ban ve
                        foreach (var id in allBlocks)
                        {
                            if (tr.GetObject(id, OpenMode.ForRead) is BlockReference blkRef)
                            {
                                string blkName = blkRef.GetEffectiveBlockName(tr);
                                if (!string.Equals(blkName, ElectricalConfig.BlockRelayFrame, StringComparison.OrdinalIgnoreCase)) continue;

                                string? frameName = blkRef.GetAttributeValue(tr, ElectricalConfig.TagRelayFrameName, "NAME");
                                if (string.Equals(frameName, dev.Name, StringComparison.OrdinalIgnoreCase))
                                {
                                    if (!blkRef.IsWriteEnabled)
                                    {
                                        blkRef.UpgradeOpen();
                                    }
                                    existingFrame = blkRef;
                                    break;
                                }
                            }
                        }
                    }

                    if (existingFrame != null)
                    {
                        FillRelayFrameAddresses(existingFrame, contacts, dev.Name, ed, tr);
                        ed.WriteMessage($"\n   -> '{dev.Name}': Da cap nhat KHUNG 14 CHAN co san.");
                    }
                    else
                    {
                        devicesNeedNewFrame.Add(dev.Name);
                        ed.WriteMessage($"\n   -> '{dev.Name}': Chua co KHUNG 14 CHAN, se tao moi.");
                    }
                }
            });

            // ──────────── BUOC 4: Tao moi cac block KHUNG 14 CHAN (cho cac thiet bi chua co) ────────────
            if (devicesNeedNewFrame.Count > 0)
            {
                ed.WriteMessage($"\n\nBuoc 3: Can tao moi {devicesNeedNewFrame.Count} block '{ElectricalConfig.BlockRelayFrame}' cho: {string.Join(", ", devicesNeedNewFrame)}");
                ed.WriteMessage("\n        Di chuyen chuot de xem truoc vi tri block, nhap chuot de dat lan luot tung block (ESC de dung).");

                ObjectId btrId = ObjectId.Null;
                CadDatabaseHelper.RunTransaction((tr, db) =>
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    if (bt.Has(ElectricalConfig.BlockRelayFrame))
                    {
                        btrId = bt[ElectricalConfig.BlockRelayFrame];
                    }
                });

                if (btrId == ObjectId.Null)
                {
                    ed.WriteMessage($"\n[LOI] Block Definition '{ElectricalConfig.BlockRelayFrame}' chua ton tai trong ban ve!");
                    ed.WriteMessage($"\n      Vui long tao hoac chen block '{ElectricalConfig.BlockRelayFrame}' vao ban ve truoc.");
                    return;
                }

                int createdCount = 0;

                for (int i = 0; i < devicesNeedNewFrame.Count; i++)
                {
                    string devName = devicesNeedNewFrame[i];
                    if (!deviceContacts.ContainsKey(devName)) continue;
                    var contacts = deviceContacts[devName];

                    // Chuan bi du lieu Attribute
                    var attrValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        { ElectricalConfig.TagRelayFrameName, devName },
                        { ElectricalConfig.TagRelayFrameModel, "" },
                        { ElectricalConfig.TagRelayFrameCoil, "" },
                    };

                    foreach (var addrTag in ElectricalConfig.TagRelayFrameAddresses)
                    {
                        attrValues[addrTag] = "-";
                    }

                    // Dien dia chi
                    foreach (var contact in contacts)
                    {
                        int addrIndex = ResolveAddressIndex(contact.PinPair);
                        if (addrIndex >= 0 && addrIndex < ElectricalConfig.TagRelayFrameAddresses.Length)
                        {
                            string addrTag = ElectricalConfig.TagRelayFrameAddresses[addrIndex];
                            string currentVal = attrValues.ContainsKey(addrTag) ? attrValues[addrTag] : "-";

                            if (currentVal != "-" && !string.IsNullOrWhiteSpace(currentVal))
                                attrValues[addrTag] = currentVal + "," + contact.Address;
                            else
                                attrValues[addrTag] = contact.Address;
                        }
                    }

                    // Chon vi tri bang Jig (truc quan, di chuyen theo chuot)
                    Point3d? pickedPt = null;
                    string promptMsg = $"\n[{i + 1}/{devicesNeedNewFrame.Count}] Nhap chuot chon vi tri dat KHUNG 14 CHAN cho '{devName}' (ESC de dung): ";

                    try
                    {
                        using (var tempBlk = new BlockReference(Point3d.Origin, btrId))
                        {
                            var jig = new BlockPlacementJig(tempBlk, Point3d.Origin, promptMsg);
                            var dragRes = ed.Drag(jig);
                            if (dragRes.Status == PromptStatus.OK)
                            {
                                pickedPt = jig.Position;
                            }
                            else
                            {
                                ed.WriteMessage($"\n[TKCRL] Da dung dat cac block con lai.");
                                break;
                            }
                        }
                    }
                    catch
                    {
                        // Fallback sang GetPoint neu Jig gap su co ve giao dien
                        var ppo = new PromptPointOptions(promptMsg);
                        var ppr = ed.GetPoint(ppo);
                        if (ppr.Status == PromptStatus.OK)
                        {
                            pickedPt = ppr.Value;
                        }
                        else
                        {
                            ed.WriteMessage($"\n[TKCRL] Da dung dat cac block con lai.");
                            break;
                        }
                    }

                    if (pickedPt.HasValue)
                    {
                        Point3d insertPt = pickedPt.Value;
                        CadDatabaseHelper.RunTransaction((tr, db) =>
                        {
                            BlockDefinitionHelper.InsertBlock(db, tr, ElectricalConfig.BlockRelayFrame, insertPt, attrValues);
                        });

                        createdCount++;
                        int filledCount = contacts.Count(c => ResolveAddressIndex(c.PinPair) >= 0);
                        ed.WriteMessage($"\n   -> [{createdCount}/{devicesNeedNewFrame.Count}] Da dat '{devName}' tai ({insertPt.X:F1}, {insertPt.Y:F1}), dien {filledCount} dia chi.");
                    }
                }

                ed.WriteMessage($"\n\n[TKCRL] HOAN TAT! Da tao va dat {createdCount}/{devicesNeedNewFrame.Count} block '{ElectricalConfig.BlockRelayFrame}'.");
            }

            ed.WriteMessage("\n[TKCRL] Ket thuc lenh thong ke chan so thiet bi.\n");
        }


        /// <summary>
        /// SYNCRL : Cập nhật lại toàn bộ địa chỉ ADDRESS trên tất cả block KHUNG 14 CHÂN tự động
        /// </summary>
        /// <summary>
        /// SYNCRL : Cập nhật lại toàn bộ địa chỉ ADDRESS trên tất cả block KHUNG 14 CHÂN và tiếp điểm tự động
        /// </summary>
        [CommandMethod("SYNCRL")]
        public void SyncRelayPins()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n--- DONG BO TOAN BO DIA CHI CHAN SO THIET BI (SYNCRL) ---");

            // Quet chon khung ten (nhan Enter de tu dong quet toan bo)
            var filter = SelectionHelper.CreateTypeFilter("INSERT");
            var selRes = ed.GetSelection(new PromptSelectionOptions
            {
                MessageForAdding = "\nQuet chon cac khung ten (nhan ENTER de tu dong quet TOAN BO ban ve): "
            }, filter);

            int totalRelays = 0;
            int totalContacts = 0;

            CadDatabaseHelper.RunTransaction((tr, db) =>
            {
                List<SheetFrame> frames;
                if (selRes.Status == PromptStatus.OK && selRes.Value != null && selRes.Value.Count > 0)
                {
                    var selectedIds = selRes.Value.Cast<SelectedObject>().Select(x => x.ObjectId);
                    var sheetNoTags = new[] { ElectricalConfig.TagSheetNumber, "A00", "SHEET_NO", "SHEET", "PAGE" };
                    frames = SheetFrameHelper.ScanSelectedFrames(tr, selectedIds, sheetNoTags);
                }
                else
                {
                    frames = SheetFrameHelper.ScanAllFramesAuto(db, tr);
                }

                if (frames.Count == 0)
                {
                    ed.WriteMessage("\n[SYNCRL] Khong tim thay khung ten nao co Tag so trang tren ban ve.");
                    return;
                }

                ed.WriteMessage($"\n -> {frames.Count} khung ten da nhan dien: Trang {string.Join(", ", frames.Select(f => f.SheetNo))}");

                // 1. Dong bo dia chi cuon coil vao tag ADDRESS_COIL cua tat ca tiep diem tren toan ban ve
                var (coilsFound, contactsUpdated, details) = SyncCoilAddressesToContacts(tr, db, frames);
                ed.WriteMessage($"\n -> Da cap nhat dia chi cuon coil (ADDRESS_COIL) cho {contactsUpdated} tiep diem tuong ung {coilsFound} cuon coil.");

                // 2. Tim tat ca block KHUNG 14 CHAN va cap nhat ADDRESS_1..4
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

                if (relayFrames.Count > 0)
                {
                    ed.WriteMessage($"\n -> Tim thay {relayFrames.Count} block KHUNG 14 CHAN. Dang cap nhat...");

                    foreach (var (relayFrame, relayName) in relayFrames)
                    {
                        if (!relayFrame.IsWriteEnabled)
                        {
                            relayFrame.UpgradeOpen();
                        }

                        // Reset ADDRESS
                        foreach (var addrTag in ElectricalConfig.TagRelayFrameAddresses)
                        {
                            relayFrame.SetAttributeValue(addrTag, "-", tr);
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
                                    string currentVal = relayFrame.GetAttributeValue(addrTag, tr) ?? "-";

                                    if (currentVal != "-" && !string.IsNullOrWhiteSpace(currentVal))
                                        relayFrame.SetAttributeValue(addrTag, currentVal + "," + address, tr);
                                    else
                                        relayFrame.SetAttributeValue(addrTag, address, tr);

                                    totalContacts++;
                                }
                            }
                        }

                        totalRelays++;
                        ed.WriteMessage($"\n   Relay '{relayName}': Da cap nhat KHUNG 14 CHAN xong.");
                    }
                }

                ed.WriteMessage($"\n\n[SYNCRL] HOAN TAT DONG BO 2 CHIEU!");
                ed.WriteMessage($"\n - Da cap nhat dia chi cuon coil (ADDRESS_COIL) cho {contactsUpdated} tiep diem.");
                if (relayFrames.Count > 0)
                {
                    ed.WriteMessage($"\n - Da cap nhat {totalContacts} dia chi tiep diem len {totalRelays} block KHUNG 14 CHAN.");
                }
            });
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

        /// <summary>
        /// Đồng bộ địa chỉ cuộn coil (Coil Address) vào attribute ADDRESS_COIL của tất cả tiếp điểm tương ứng
        /// </summary>
        public static (int CoilsFound, int ContactsUpdated, Dictionary<string, List<string>> UpdatedDetails)
            SyncCoilAddressesToContacts(
                Transaction tr,
                Database db,
                List<SheetFrame> frames,
                IEnumerable<ObjectId>? targetBlockIds = null,
                IEnumerable<DeviceInfo>? knownDevices = null,
                IEnumerable<string>? filterDevices = null)
        {
            var filterSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (filterDevices != null)
            {
                foreach (var f in filterDevices) filterSet.Add(f);
            }
            if (knownDevices != null)
            {
                foreach (var d in knownDevices) filterSet.Add(d.Name);
            }

            // Map: DeviceName -> (Point3d Pos, string Address)
            var coilsMap = new Dictionary<string, (Point3d Pos, string Address)>(StringComparer.OrdinalIgnoreCase);
            var relayFramesFallback = new Dictionary<string, (Point3d Pos, string Address)>(StringComparer.OrdinalIgnoreCase);

            // 1a. Nạp từ knownDevices (ví dụ từ bước 1 của TKCRL)
            if (knownDevices != null)
            {
                foreach (var dev in knownDevices)
                {
                    if (dev.CoilPosition != Point3d.Origin || dev.CoilBlockId != ObjectId.Null)
                    {
                        var frame = SheetFrameHelper.FindFrameContaining(frames, dev.CoilPosition);
                        string addr = frame != null
                            ? frame.GetAddress(dev.CoilPosition, ElectricalConfig.FrameRows, ElectricalConfig.FrameColumns, ElectricalConfig.ColumnLetters)
                            : "-";
                        coilsMap[dev.Name] = (dev.CoilPosition, addr);
                    }
                    else if (dev.ExistingFrameId != ObjectId.Null)
                    {
                        var frame = SheetFrameHelper.FindFrameContaining(frames, dev.CoilPosition);
                        string addr = frame != null
                            ? frame.GetAddress(dev.CoilPosition, ElectricalConfig.FrameRows, ElectricalConfig.FrameColumns, ElectricalConfig.ColumnLetters)
                            : "-";
                        relayFramesFallback[dev.Name] = (dev.CoilPosition, addr);
                    }
                }
            }

            var allBlockIds = SelectionHelper.GetAllEntitiesOfType<BlockReference>(db, tr);
            var contactsToUpdate = new List<(BlockReference ContactRef, string DeviceName)>();

            // 1b. Quét toàn bộ block trên bản vẽ
            foreach (var id in allBlockIds)
            {
                if (tr.GetObject(id, OpenMode.ForRead) is BlockReference blkRef)
                {
                    string blkName = blkRef.GetEffectiveBlockName(tr);
                    bool isRelayFrame = string.Equals(blkName, ElectricalConfig.BlockRelayFrame, StringComparison.OrdinalIgnoreCase);

                    string? name = blkRef.GetAttributeValue(tr, ElectricalConfig.TagDeviceName, ElectricalConfig.TagRelayFrameName, "TAG", "NAME", "DEVICE");
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    if (filterSet.Count > 0 && !filterSet.Contains(name!)) continue;

                    if (isRelayFrame)
                    {
                        if (!relayFramesFallback.ContainsKey(name!))
                        {
                            var frame = SheetFrameHelper.FindFrameContaining(frames, blkRef.Position);
                            string addr = frame != null
                                ? frame.GetAddress(blkRef.Position, ElectricalConfig.FrameRows, ElectricalConfig.FrameColumns, ElectricalConfig.ColumnLetters)
                                : "-";
                            relayFramesFallback[name!] = (blkRef.Position, addr);
                        }
                        continue;
                    }

                    // Kiểm tra xem block có attribute ADDRESS_COIL (tiếp điểm) không
                    bool hasAddressCoilTag = false;
                    foreach (ObjectId attId in blkRef.AttributeCollection)
                    {
                        if (tr.GetObject(attId, OpenMode.ForRead) is AttributeReference attRef)
                        {
                            if (ElectricalConfig.TagAddressContactAliases.Any(t => string.Equals(attRef.Tag, t, StringComparison.OrdinalIgnoreCase)))
                            {
                                hasAddressCoilTag = true;
                                break;
                            }
                        }
                    }

                    if (hasAddressCoilTag)
                    {
                        // Là tiếp điểm
                        contactsToUpdate.Add((blkRef, name!));
                    }
                    else
                    {
                        // Là cuộn hút Coil
                        var frame = SheetFrameHelper.FindFrameContaining(frames, blkRef.Position);
                        string coilAddr = frame != null
                            ? frame.GetAddress(blkRef.Position, ElectricalConfig.FrameRows, ElectricalConfig.FrameColumns, ElectricalConfig.ColumnLetters)
                            : "-";

                        // Ưu tiên block có tên chứa 'COIL' hoặc lưu nếu chưa có
                        if (!coilsMap.ContainsKey(name!) || blkName.IndexOf("COIL", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            coilsMap[name!] = (blkRef.Position, coilAddr);
                        }
                    }
                }
            }

            // 1c. Nếu thiết bị nào chưa có block cuộn coil thì fallback sang vị trí KHUNG 14 CHAN
            foreach (var kvp in relayFramesFallback)
            {
                if (!coilsMap.ContainsKey(kvp.Key))
                {
                    coilsMap[kvp.Key] = kvp.Value;
                }
            }

            // 2. Ghi địa chỉ cuộn coil vào attribute ADDRESS_COIL của các tiếp điểm
            HashSet<ObjectId>? targetSet = targetBlockIds != null ? new HashSet<ObjectId>(targetBlockIds) : null;
            int contactsUpdated = 0;
            var updatedDetails = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var (contactRef, devName) in contactsToUpdate)
            {
                if (targetSet != null && !targetSet.Contains(contactRef.ObjectId)) continue;

                if (coilsMap.TryGetValue(devName, out var coilInfo))
                {
                    if (string.IsNullOrWhiteSpace(coilInfo.Address) || coilInfo.Address == "-")
                    {
                        continue;
                    }

                    bool written = contactRef.SetAttributeValue(tr, coilInfo.Address, ElectricalConfig.TagAddressContactAliases);
                    if (written)
                    {
                        contactsUpdated++;
                        string p1 = contactRef.GetAttributeValue(tr, ElectricalConfig.TagPin1, "TERM01", "PIN1") ?? "";
                        string p2 = contactRef.GetAttributeValue(tr, ElectricalConfig.TagPin2, "TERM02", "PIN2") ?? "";
                        string pinPair = string.IsNullOrWhiteSpace(p2) ? p1 : $"{p1}-{p2}";

                        if (!updatedDetails.ContainsKey(devName))
                            updatedDetails[devName] = new List<string>();

                        string desc = string.IsNullOrWhiteSpace(pinPair)
                            ? coilInfo.Address
                            : $"chân [{pinPair}] -> {coilInfo.Address}";
                        updatedDetails[devName].Add(desc);
                    }
                }
            }

            return (coilsMap.Count, contactsUpdated, updatedDetails);
        }

        /// <summary>
        /// DSTTKBT : Đánh số thứ tự khung tên bản vẽ (với UI cài đặt)
        ///
        /// QUY TRÌNH:
        ///  1. Hiện form cài đặt (Prefix, Suffix, From, kiểu đánh số, hướng, chế độ trang)
        ///  2. Người dùng nhấn "Chọn blocks" hoặc OK
        ///  3. Quét chọn các block khung tên trên bản vẽ
        ///  4. Sắp xếp theo hướng đã chọn
        ///  5. Gán giá trị số trang theo quy tắc đã cài đặt
        ///  6. Nếu chế độ trang: ghi dạng "12/24" (trang hiện tại / tổng trang)
        /// </summary>
        [CommandMethod("DSTTKBT")]
        public void SheetNumberingWithUI()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n--- DANH SO THU TU KHUNG TEN BAN VE (DSTTKBT) ---");

            // 1. Hien thi form cai dat
            SheetNumberingForm? form = null;
            bool usePageMode = false;
            int totalPages = 24;
            string prefix = "";
            string suffix = "";
            int startNum = 1;
            bool twoDigits = true;
            int styleIndex = 0;
            int dirIndex = 0;
            string targetBlockName = "";
            string targetAttrTag = "";

            Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(
                Autodesk.AutoCAD.ApplicationServices.Application.MainWindow.Handle,
                form = new SheetNumberingForm()
            );

            if (form.DialogResult != DialogResult.OK)
            {
                ed.WriteMessage("\nDa huy lenh danh so thu tu.");
                return;
            }

            prefix = form.NumberPrefix;
            suffix = form.NumberSuffix;
            startNum = form.StartNumber;
            twoDigits = form.UseTwoDigits;
            styleIndex = form.NumberStyleIndex;
            dirIndex = form.DirectionIndex;
            targetBlockName = form.TargetBlockName;
            targetAttrTag = form.TargetAttrTag;
            usePageMode = form.UsePageMode;
            bool autoTotal = form.AutoTotalPages;
            totalPages = form.TotalPages;
            string totalPagesTag = form.TotalPagesAttrTag;
            string totalPrefix = form.TotalPagesPrefix;

            if (string.IsNullOrWhiteSpace(targetAttrTag))
            {
                ed.WriteMessage("\n[LOI] Chua nhap Attribute TAG. Huy lenh.");
                return;
            }

            // 2. Quet chon cac block khung ten
            ed.WriteMessage($"\nQuet chon cac block '{targetBlockName}' can danh so:");

            var filter = SelectionHelper.CreateTypeFilter("INSERT");
            var selRes = ed.GetSelection(new PromptSelectionOptions
            {
                MessageForAdding = $"\nQuet chon cac block khung ten (co the chon nhieu): "
            }, filter);

            if (selRes.Status != PromptStatus.OK || selRes.Value == null || selRes.Value.Count == 0)
            {
                ed.WriteMessage("\nChua chon block nao. Huy lenh.");
                return;
            }

            // 3. Doc va sap xep cac block theo huong da chon
            int numberedCount = 0;
            string summaryTotalValue = "";

            CadDatabaseHelper.RunTransaction((tr, db) =>
            {
                var blockList = new List<(BlockReference blk, Point3d pos)>();

                foreach (SelectedObject selObj in selRes.Value)
                {
                    if (selObj == null) continue;
                    if (tr.GetObject(selObj.ObjectId, OpenMode.ForWrite) is BlockReference blkRef)
                    {
                        // Loc theo ten block (neu nguoi dung da nhap)
                        if (!string.IsNullOrWhiteSpace(targetBlockName))
                        {
                            string blkName = blkRef.GetEffectiveBlockName(tr);
                            if (!string.Equals(blkName, targetBlockName, StringComparison.OrdinalIgnoreCase))
                                continue;
                        }

                        blockList.Add((blkRef, blkRef.Position));
                    }
                }

                if (blockList.Count == 0)
                {
                    ed.WriteMessage($"\n[LOI] Khong tim thay block '{targetBlockName}' nao trong vung chon.");
                    return;
                }

                // Sap xep theo huong
                switch (dirIndex)
                {
                    case 0: // Trai => Phai | Tren => Duoi
                        blockList = blockList
                            .OrderByDescending(b => b.pos.Y) // Tren truoc
                            .ThenBy(b => b.pos.X)            // Trai truoc
                            .ToList();
                        break;
                    case 1: // Trai => Phai | Duoi => Tren
                        blockList = blockList
                            .OrderBy(b => b.pos.Y)           // Duoi truoc
                            .ThenBy(b => b.pos.X)
                            .ToList();
                        break;
                    case 2: // Tren => Duoi | Trai => Phai
                        blockList = blockList
                            .OrderBy(b => b.pos.X)
                            .ThenByDescending(b => b.pos.Y)
                            .ToList();
                        break;
                }

                // Tinh tong so trang thuc te
                int actualTotal = autoTotal ? blockList.Count : Math.Max(totalPages, blockList.Count);
                string totalStr = SheetNumberingForm.FormatNumber(actualTotal, styleIndex, twoDigits);
                string totalValue = $"{totalPrefix}{totalStr}";
                summaryTotalValue = totalValue;

                // 4. Gan so thu tu
                for (int i = 0; i < blockList.Count; i++)
                {
                    int currentNumber = startNum + i;
                    string numStr = SheetNumberingForm.FormatNumber(currentNumber, styleIndex, twoDigits);
                    string pageValue = $"{prefix}{numStr}{suffix}";

                    // Gan so trang vao Tag chi dinh (vi du: A00)
                    bool pageWritten = blockList[i].blk.SetAttributeValue(targetAttrTag, pageValue, tr);
                    if (pageWritten)
                    {
                        numberedCount++;
                    }
                    else
                    {
                        ed.WriteMessage($"\n   [{i + 1}] [CANH BAO] Khong tim thay Tag '{targetAttrTag}' trong block.");
                    }

                    // Neu bat che do cap nhat tong trang, gan vao Tag tong (vi du: TSHEET)
                    if (usePageMode && !string.IsNullOrWhiteSpace(totalPagesTag))
                    {
                        bool totalWritten = blockList[i].blk.SetAttributeValue(totalPagesTag, totalValue, tr);
                        if (!totalWritten)
                        {
                            ed.WriteMessage($"\n   [{i + 1}] [CANH BAO] Khong tim thay Tag tong '{totalPagesTag}' trong block.");
                        }
                    }

                    if (usePageMode && !string.IsNullOrWhiteSpace(totalPagesTag))
                    {
                        ed.WriteMessage($"\n   [{i + 1}] -> {targetAttrTag}='{pageValue}', {totalPagesTag}='{totalValue}'");
                    }
                    else
                    {
                        ed.WriteMessage($"\n   [{i + 1}] -> {targetAttrTag}='{pageValue}'");
                    }
                }
            });

            ed.WriteMessage($"\n\n[DSTTKBT] HOAN TAT! Da danh so {numberedCount} khung ten.");
            if (usePageMode && !string.IsNullOrWhiteSpace(totalPagesTag))
            {
                ed.WriteMessage($" (Trang: Tag '{targetAttrTag}', Tong trang: Tag '{totalPagesTag}' = '{summaryTotalValue}')");
            }
        }
    }
}
