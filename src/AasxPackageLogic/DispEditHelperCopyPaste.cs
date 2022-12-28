﻿/*
Copyright (c) 2018-2019 Festo AG & Co. KG <https://www.festo.com/net/de_de/Forms/web/contact_international>
Author: Michael Hoffmeister

This source code is licensed under the Apache License 2.0 (see LICENSE.txt).

This source code may use other Open Source software components (see LICENSE.txt).
*/

using System;
using System.Collections.Generic;
using System.Linq;
using AasCore.Aas3_0_RC02;
using AasxIntegrationBase;
using AasxIntegrationBase.AdminShellEvents;
using AdminShellNS;
using AdminShellNS.Display;
using AdminShellNS.Extenstions;
using AnyUi;
using Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AasxPackageLogic
{
    /// <summary>
    /// This class extends the basic helper functionalities of DispEditHelper by providing functionality
    /// for cut/copy/paste of different elements
    /// </summary>
    public class DispEditHelperCopyPaste : DispEditHelperBasics
    {
        //
        // Data structures
        //

        public class CopyPasteItemBase
        {
            // members for all

            public virtual object GetMainDataObject() { return null; }

            // Factory

            public static CopyPasteItemBase FactoryConvertFrom(IReferable rf)
            {
                // try fake a copy paste item (order matters!)
                CopyPasteItemBase res = CopyPasteItemSME.ConvertFrom(rf);
                if (res == null)
                    res = CopyPasteItemSubmodel.ConvertFrom(rf);
                if (res == null)
                    res = CopyPasteItemIdentifiable.ConvertFrom(rf);

                // ok
                return res;
            }
        }

        public class ListOfCopyPasteItem : List<CopyPasteItemBase>
        {
            public ListOfCopyPasteItem() { }

            public ListOfCopyPasteItem(CopyPasteItemBase item)
            {
                this.Add(item);
            }

            /// <summary>
            /// Check, if elements are present and all of same type.
            /// </summary>
            /// <typeparam name="T">Desired subclass of <c>VisualElementGeneric</c></typeparam>
            public bool AllOfElementType<T>() where T : CopyPasteItemBase
            {
                if (this.Count < 1)
                    return false;
                foreach (var ve in this)
                    if (!(ve is T))
                        return false;
                return true;
            }
        }

        public class CopyPasteItemIdentifiable : CopyPasteItemBase
        {
            public object parentContainer = null;
            public IIdentifiable entity = null;

            public override object GetMainDataObject() { return entity; }

            public CopyPasteItemIdentifiable() { }

            public CopyPasteItemIdentifiable(
                object parentContainer,
                IIdentifiable entity)
            {
                this.parentContainer = parentContainer;
                this.entity = entity;
            }

            public static CopyPasteItemIdentifiable ConvertFrom(IReferable rf)
            {
                // access
                var idf = rf as IIdentifiable;
                if (idf == null
                    || !(idf is AssetAdministrationShell
                         || idf is AssetInformation || idf is ConceptDescription))
                    return null;

                // create
                return new CopyPasteItemIdentifiable()
                {
                    entity = idf
                };
            }
        }

        public class CopyPasteItemSubmodel : CopyPasteItemBase
        {
            public object parentContainer = null;
            public Reference smref = null;
            public Submodel sm = null;

            public override object GetMainDataObject() { return sm; }

            public CopyPasteItemSubmodel() { }

            public CopyPasteItemSubmodel(
                object parentContainer,
                object entity,
                Reference smref,
                Submodel sm)
            {
                this.parentContainer = parentContainer;
                this.smref = smref;
                this.sm = sm;
                TryFixSmRefIfNull();
            }

            public void TryFixSmRefIfNull()
            {
                if (smref == null && sm?.Id != null)
                {
                    smref = new Reference(ReferenceTypes.ModelReference, new List<Key>() { new Key(KeyTypes.Submodel, sm.Id) });
                }
            }

            public static CopyPasteItemSubmodel ConvertFrom(IReferable rf)
            {
                // access
                var sm = rf as Submodel;
                if (sm == null || sm.Id == null)
                    return null;

                // create
                var res = new CopyPasteItemSubmodel() { sm = sm };

                // fake smref
                res.TryFixSmRefIfNull();

                // ok
                return res;
            }
        }

        public class CopyPasteItemSME : CopyPasteItemBase
        {
            public AasCore.Aas3_0_RC02.Environment env = null;
            public IReferable parentContainer = null;
            public ISubmodelElement wrapper = null;
            public ISubmodelElement sme = null;
            public EnumerationPlacmentBase Placement = null;

            public override object GetMainDataObject() { return sme; }


            public CopyPasteItemSME() { }

            public CopyPasteItemSME(
                AasCore.Aas3_0_RC02.Environment env,
                IReferable parentContainer, ISubmodelElement wrapper,
                ISubmodelElement sme,
                EnumerationPlacmentBase placement = null)
            {
                this.env = env;
                this.parentContainer = parentContainer;
                this.wrapper = wrapper;
                this.sme = sme;
                this.Placement = placement;
            }

            public static CopyPasteItemSME ConvertFrom(IReferable rf)
            {
                // access
                var sme = rf as ISubmodelElement;
                if (sme == null)
                    return null;

                // new wrapper
                ISubmodelElement wrapper;
                wrapper = sme;

                // create
                return new CopyPasteItemSME()
                {
                    sme = sme,
                    wrapper = wrapper
                };
            }
        }

        public class CopyPasteBuffer
        {
            public bool Valid = false;
            public bool ExternalSource = false;
            public bool Duplicate = false;

            public string Watermark = null;

            public ListOfCopyPasteItem Items;

            public bool ContentAvailable { get { return Items != null && Items.Count > 0 && Valid; } }

            public CopyPasteBuffer()
            {
                GenerateWatermark();
            }

            public void GenerateWatermark()
            {
                var r = new Random();
                Watermark = String.Format("{0:000000}", r.Next(1, 999999));
            }

            public void Clear()
            {
                this.Valid = false;
                this.ExternalSource = false;
                this.Duplicate = false;
                GenerateWatermark();
                this.Items = null;
            }

            public static Tuple<string[], List<Key>[]> PreparePresetsForListKeys(
                CopyPasteBuffer cpb, string label = "Paste")
            {
                // add from Copy Buffer
                List<Key> bufferKey = null;
                if (cpb != null && cpb.Valid && cpb.Items != null && cpb.Items.Count == 1)
                {
                    if (cpb.Items[0] is CopyPasteItemIdentifiable cpbi && cpbi.entity?.Id != null)
                        bufferKey = new List<Key>() { new Key((KeyTypes)Stringification.KeyTypesFromString(cpbi.entity.GetSelfDescription().AasElementName), cpbi.entity.Id)};

                    if (cpb.Items[0] is CopyPasteItemSubmodel cpbsm && cpbsm.sm?.SemanticId != null)
                        //bufferKey = List<Key>.CreateNew(cpbsm.sm.GetReference()?.First);
                        bufferKey = new List<Key>() { cpbsm.sm.GetReference().Keys.First()};

                    if (cpb.Items[0] is CopyPasteItemSME cpbsme && cpbsme.sme != null
                        && cpbsme.env.Submodels != null)
                    {
                        // index parents for ALL Submodels -> parent for our SME shall be set by this ..
                        foreach (var sm in cpbsme.env?.Submodels)
                            sm?.SetAllParents();

                        // collect buffer list
                        bufferKey = new List<Key>();
                        cpbsme.sme.CollectReferencesByParent(bufferKey);
                    }
                }

                // result
                return new Tuple<string[], List<Key>[]>(
                    (bufferKey == null) ? null : new[] { label },
                    (bufferKey == null) ? null : new[] { bufferKey }
                );
            }

            private string PrepareClipboadString()
            {
#if V20
                // make a pure array of objects
                // (absolutely no unnecessary stuff to the public)
                var oar = new List<object>();
                if (Items != null)
                    foreach (var it in Items)
                    {
                        var o = it.GetMainDataObject();
                        if (o != null)
                            oar.Add(o);
                    }

                // nothing? what to serialize?
                if (oar.Count < 1)
                    return null;
                var objToSerialize = (oar.Count == 1) ? oar[0] : oar;

                // make JSON
                var settings = AasxIntegrationBase.AasxPluginOptionSerialization.GetDefaultJsonSettings(
                    new[] { typeof(AasxIntegrationBase.AdminShellEvents.AasEventMsgEnvelope) });
                settings.TypeNameHandling = TypeNameHandling.None;
                settings.Formatting = Formatting.Indented;
                var json = JsonConvert.SerializeObject(objToSerialize, settings);
#else
                var oar = new List<IClass>();
                if (Items != null)
                    foreach (var it in Items)
                    {
                        var o = it.GetMainDataObject();
                        if (o is IClass oir)
                            oar.Add(oir);
                    }

                var nodes = ExtendIReferable.ToJsonObject(oar);
                var json = nodes.ToJsonString(
                    new System.Text.Json.JsonSerializerOptions()
                    {
                        WriteIndented = true
                    });
#endif

                // ok
                return json;
            }

            public void CopyToClipboard(AnyUiContextBase context, string watermark = null)
            {
                // access
                if (context == null)
                    return;

                // get something?
                var s = PrepareClipboadString();
                if (s == null)
                    return;

                // prepare clipboard data
                context.ClipboardSet(new AnyUiClipboardData()
                {
                    Watermark = watermark,
                    Text = s
                });
            }

            public static CopyPasteBuffer FromClipboardString(string cps)
            {
                // access
                if (cps == null)
                    return null;
                cps = cps.Trim();
                if (cps.Length < 1)
                    return null;

                // quite likely to crash
                try
                {
                    // be very straight for allowed formats
                    var isSingleObject = cps.StartsWith("{");
                    var isArrayObject = cps.StartsWith("[");

                    // try simple way
                    if (isSingleObject)
                    {
                        // TODO (MIHO, 2021-06-22): think of converting IReferable to IAasElement
                        var obj = AdminShellSerializationHelper.DeserializeFromJSON<IReferable>(cps);

                        // try fake a copy paste item (order matters!)
                        var cpi = CopyPasteItemBase.FactoryConvertFrom(obj);
                        if (cpi != null)
                            return new CopyPasteBuffer()
                            {
                                Valid = true,
                                ExternalSource = true,
                                Duplicate = true,
                                Items = new ListOfCopyPasteItem(cpi)
                            };
                    }
                    else
                    if (isArrayObject)
                    {
                        // make array of object
                        //var objarr = AdminShellSerializationHelper
                        //    .DeserializePureObjectFromJSON<List<IReferable>>(cps);
                        var node = System.Text.Json.Nodes.JsonNode.Parse(cps);
                        var objarr = ExtendIReferable.ListOfIReferableFrom(node);
                        if (objarr != null)
                        {
                            // overall structure
                            var cpb = new CopyPasteBuffer()
                            {
                                Valid = true,
                                ExternalSource = true,
                                Duplicate = true,
                                Items = new ListOfCopyPasteItem()
                            };

                            // single items
                            foreach (var obj in objarr)
                            {
                                var cpi = CopyPasteItemBase.FactoryConvertFrom(obj);
                                if (cpi != null)
                                    cpb.Items.Add(cpi);
                            }

                            // be picky with validity
                            if (cpb.Items.Count > 0)
                                return cpb;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Singleton.Error(ex, "when trying to decode clipboad text");
                }

                // ups
                return null;
            }

            /// <summary>
            /// Either returns itself as valid copy paste buffer or adopts an temporary one
            /// by taking the clipboard data.
            /// </summary>
            public CopyPasteBuffer CheckIfUseExternalCopyPasteBuffer(AnyUiClipboardData cbd)
            {
                // easy way out
                var res = this;
                if (cbd == null)
                    return res;

                // try get external one
                var ext = FromClipboardString(cbd.Text);
                if (ext == null)
                    // default way out
                    return res;

                // if this buffer is not valid, but clipboard can provide? 
                if (!this.Valid && ext.Valid)
                    return ext;

                // ok, default is us
                return res;
            }
        }

        //
        // Helper functions
        //

        protected void DispDeleteCopyPasteItem(CopyPasteItemSME item)
        {
            // access
            if (item == null)
                return;

            // differentiate
            if (item.parentContainer is Submodel pcsm && item.wrapper != null)
                this.DeleteElementInList<ISubmodelElement>(
                    pcsm.SubmodelElements, item.wrapper, null);

            if (item.parentContainer is SubmodelElementCollection pcsmc
                && item.wrapper != null)
                this.DeleteElementInList<ISubmodelElement>(
                    pcsmc.Value, item.wrapper, null);

            if (item.parentContainer is Operation pcop && item.wrapper != null)
            {
                var placement = pcop.GetChildrenPlacement(item.sme) as
                    EnumerationPlacmentOperationVariable;
                if (placement != null)
                    //pcop[placement.Direction].Remove(placement.OperationVariable);
                {
                    if(placement.Direction == OperationVariableDirection.In)
                    {
                        pcop.InputVariables.Remove(placement.OperationVariable);
                    }
                    else if(placement.Direction == OperationVariableDirection.Out)
                    {
                        pcop.OutputVariables.Remove(placement.OperationVariable);
                    }
                    else if(placement.Direction == OperationVariableDirection.InOut)
                    {
                        pcop.InoutputVariables.Remove(placement.OperationVariable);
                    }
                }
                   
            }
        }

        public void DispSmeCutCopyPasteHelper(
            AnyUiPanel stack,
            ModifyRepo repo,
            AasCore.Aas3_0_RC02.Environment env,
            IReferable parentContainer,
            CopyPasteBuffer cpbInternal,
            ISubmodelElement wrapper,
            ISubmodelElement sme,
            string label = "Buffer:")
        {
            // access
            if (parentContainer == null || cpbInternal == null || sme == null)
                return;

            // use an action
            this.AddAction(
                stack, label,
                new[] { "Cut", "Copy", "Paste above", "Paste below", "Paste into" }, repo,
                actionTags: new[] { "aas-elem-cut", "aas-elem-copy", "aas-elem-paste-above",
                    "aas-elem-paste-below", "aas-elem-paste-into" },
                action: (buttonNdx) =>
                {
                    if (buttonNdx == 0 || buttonNdx == 1)
                    {
                        // store info
                        cpbInternal.Clear();
                        cpbInternal.Valid = true;
                        cpbInternal.Duplicate = buttonNdx == 1;
                        EnumerationPlacmentBase placement = null;
                        //if (parentContainer is IEnumerateChildren enc) //No IEnumerateChildren in V3
                            placement = parentContainer.GetChildrenPlacement(sme);
                        cpbInternal.Items = new ListOfCopyPasteItem(
                            new CopyPasteItemSME(env, parentContainer, wrapper, sme, placement));
                        cpbInternal.CopyToClipboard(context, cpbInternal.Watermark);

                        // special case?

                        // user feedback
                        Log.Singleton.Info(
                            StoredPrint.Color.Blue,
                            "Stored SubmodelElement '{0}'({1}) to internal buffer.{2}", "" + sme.IdShort,
                            "" + sme?.GetSelfDescription().AasElementName,
                            cpbInternal.Duplicate
                                ? " Paste will duplicate."
                                : " Paste will cut at original position.");
                    }

                    if (buttonNdx == 2 || buttonNdx == 3 || buttonNdx == 4)
                    {
                        // which buffer?
                        var cbdata = context?.ClipboardGet();
                        var cpb = cpbInternal.CheckIfUseExternalCopyPasteBuffer(cbdata);

                        // content?
                        if (!cpb.ContentAvailable)
                        {
                            this.context?.MessageBoxFlyoutShow(
                                "No sufficient infomation in internal paste buffer or external clipboard.",
                                "Copy & Paste",
                                AnyUiMessageBoxButton.OK, AnyUiMessageBoxImage.Error);
                            return new AnyUiLambdaActionNone();
                        }

                        // uniform?
                        if (!cpb.Items.AllOfElementType<CopyPasteItemSME>())
                        {
                            this.context?.MessageBoxFlyoutShow(
                                    "No (valid) information for SubmodelElements in copy/paste buffer.",
                                    "Copy & Paste",
                                    AnyUiMessageBoxButton.OK, AnyUiMessageBoxImage.Information);
                            return new AnyUiLambdaActionNone();
                        }

                        // user feedback
                        Log.Singleton.Info($"Pasting {cpb.Items.Count} SubmodelElements from paste buffer");

                        // loop over items
                        object nextBusObj = null;
                        foreach (var it in cpb.Items)
                        {
                            // access
                            var item = it as CopyPasteItemSME;
                            if (item?.sme == null || item.wrapper == null
                                || (!cpb.Duplicate && item?.parentContainer == null))
                            {
                                Log.Singleton.Error("When pasting SME, an element was invalid.");
                                continue;
                            }

                            // apply info
                            var smw2 = item.sme.Copy();
                            nextBusObj = smw2;
                            var createAtIndex = -1;

#if old
                            // make this unique (e.g. for event following)
                            if (cpb.Duplicate || cpb.ExternalSource)
                                this.MakeNewReferableUnique(smw2);
#endif
                            // make this unique later (e.g. for event following)
                            var makeUnique = (cpb.Duplicate || cpb.ExternalSource);

                            // insertation depends on parent container
                            if (buttonNdx == 2)
                            {
                                // handle parent explicitely, as not covered by AddElementInListBefore/after
                                smw2.Parent = parentContainer;

                                if (parentContainer is Submodel pcsm && wrapper != null)
                                    createAtIndex = this.AddElementInSmeListBefore<ISubmodelElement>(
                                        pcsm.SubmodelElements, smw2, wrapper);

                                if (parentContainer is SubmodelElementCollection pcsmc &&
                                        wrapper != null)
                                    createAtIndex = this.AddElementInSmeListBefore<ISubmodelElement>(
                                        pcsmc.Value, smw2, wrapper, makeUnique);

                                if (parentContainer is Entity pcent &&
                                        wrapper != null)
                                    createAtIndex = this.AddElementInSmeListBefore<ISubmodelElement>(
                                        pcent.Statements, smw2, wrapper, makeUnique);

                                if (parentContainer is AnnotatedRelationshipElement pcarel &&
                                        wrapper != null)
                                {
                                    var annotations = new List<ISubmodelElement>(pcarel.Annotations);
                                    createAtIndex = this.AddElementInSmeListBefore<ISubmodelElement>(
                                        annotations, smw2, wrapper, makeUnique);
                                }
                                    

                                // TODO (Michael Hoffmeister, 2020-08-01): Operation complete?
                                if (parentContainer is Operation pcop && wrapper != null)
                                {
                                    var place = pcop.GetChildrenPlacement(wrapper) as
                                        EnumerationPlacmentOperationVariable;
                                    if (place?.OperationVariable != null)
                                    {
                                        var op = new OperationVariable(smw2);
                                        var opVariables = pcop.GetVars(place.Direction);
                                        createAtIndex = this.AddElementInListBefore<OperationVariable>(
                                            opVariables, op, place.OperationVariable);
                                        nextBusObj = op;
                                    }
                                }
                            }

                            if (buttonNdx == 3)
                            {
                                // handle parent explicitely, as not covered by AddElementInListBefore/after
                                smw2.Parent = parentContainer;

                                if (parentContainer is Submodel pcsm && wrapper != null)
                                    createAtIndex = this.AddElementInSmeListAfter<ISubmodelElement>(
                                        pcsm.SubmodelElements, smw2, wrapper, makeUnique);

                                if (parentContainer is SubmodelElementCollection pcsmc &&
                                        wrapper != null)
                                    createAtIndex = this.AddElementInSmeListAfter<ISubmodelElement>(
                                        pcsmc.Value, smw2, wrapper, makeUnique);

                                if (parentContainer is Entity pcent && wrapper != null)
                                    createAtIndex = this.AddElementInSmeListAfter<ISubmodelElement>(
                                        pcent.Statements, smw2, wrapper, makeUnique);

                                if (parentContainer is AnnotatedRelationshipElement pcarel &&
                                        wrapper != null)
                                {
                                    var annotations = new List<ISubmodelElement>(pcarel.Annotations);
                                    createAtIndex = this.AddElementInSmeListAfter<ISubmodelElement>(
                                        annotations, smw2, wrapper, makeUnique);
                                }

                                // TODO (Michael Hoffmeister, 2020-08-01): Operation complete?
                                if (parentContainer is Operation pcop && wrapper != null)
                                {
                                    var place = pcop.GetChildrenPlacement(wrapper) as
                                        EnumerationPlacmentOperationVariable;
                                    if (place?.OperationVariable != null)
                                    {
                                        var op = new OperationVariable(smw2);
                                        var opVariables = pcop.GetVars(place.Direction);
                                        createAtIndex = this.AddElementInListAfter<OperationVariable>(
                                            opVariables, op, place.OperationVariable);
                                        nextBusObj = op;
                                    }
                                }
                            }

                            if (buttonNdx == 4)
                            {
                                if (makeUnique)
                                {
                                    var found = false;
                                    foreach (var ch in sme.DescendOnce().OfType<ISubmodelElement>())
                                        if (ch?.IdShort?.Trim() == smw2?.IdShort?.Trim())
                                            found = true;
                                    if (found)
                                        this.MakeNewReferableUnique(smw2);
                                }

                                // aprent set automatically
                                // TODO (MIHO, 2021-08-18): createAtIndex missing here
                                //if (sme is IEnumerateChildren smeec)
                                //    smeec.AddChild(smw2, item.Placement);
                                sme.AddChild(smw2, item.Placement);
                            }

                            // emit event
                            this.AddDiaryEntry(smw2,
                                new DiaryEntryStructChange(StructuralChangeReason.Create,
                                    createAtIndex: createAtIndex));

                            // may delete original
                            if (!cpb.Duplicate && !cpb.ExternalSource)
                            {
                                DispDeleteCopyPasteItem(item);

                                this.AddDiaryEntry(item.sme,
                                    new DiaryEntryStructChange(StructuralChangeReason.Delete));
                            }
                        }

                        // the buffer is tainted
                        cpb.Clear();

                        // try to focus next
                        return new AnyUiLambdaActionRedrawAllElements(
                            nextFocus: nextBusObj, isExpanded: true);
                    }

                    return new AnyUiLambdaActionNone();
                });
        }

        public void DispSubmodelCutCopyPasteHelper<T>(
            AnyUiPanel stack,
            ModifyRepo repo,
            CopyPasteBuffer cpbInternal,
            List<T> parentContainer,
            T entity,
            Func<T, T> cloneEntity,
            Reference smref,
            Submodel sm,
            string label = "Buffer:",
            Func<T, T, bool> checkEquality = null,
            Action<CopyPasteItemBase> extraAction = null) /*where T : new()*/ //TODO:jtikekar Test
        {
            // access
            if (parentContainer == null || cpbInternal == null || sm == null || cloneEntity == null)
                return;

            // integrity
            // ReSharper disable RedundantCast
            if (entity as object != smref as object && entity as object != sm as object)
                return;
            // ReSharper enable RedundantCast

            // use an action
            this.AddAction(
                stack, label,
                new[] { "Cut", "Copy", "Paste above", "Paste below", "Paste into" }, repo,
                (buttonNdx) =>
                {
                    if (buttonNdx == 0 || buttonNdx == 1)
                    {
                        // store info
                        cpbInternal.Clear();
                        cpbInternal.Valid = true;
                        cpbInternal.Duplicate = buttonNdx == 1;
                        cpbInternal.Items = new ListOfCopyPasteItem(
                            new CopyPasteItemSubmodel(parentContainer, entity, smref, sm));
                        cpbInternal.CopyToClipboard(context, cpbInternal.Watermark);

                        // user feedback
                        Log.Singleton.Info(
                            StoredPrint.Color.Blue,
                            "Stored Submodel '{0}' to internal buffer.{1}", "" + sm.IdShort,
                            cpbInternal.Duplicate
                                ? " Paste will duplicate."
                                : " Paste will cut at original position.");
                    }

                    if (buttonNdx == 2 || buttonNdx == 3)
                    {
                        // which buffer?
                        var cbdata = context?.ClipboardGet();
                        var cpb = cpbInternal.CheckIfUseExternalCopyPasteBuffer(cbdata);

                        // content?
                        if (!cpb.ContentAvailable)
                        {
                            this.context?.MessageBoxFlyoutShow(
                                "No sufficient infomation in internal paste buffer or external clipboard.",
                                "Copy & Paste",
                                AnyUiMessageBoxButton.OK, AnyUiMessageBoxImage.Error);
                            return new AnyUiLambdaActionNone();
                        }

                        // pasting above/ below means: Submodels
                        if (!cpb.Items.AllOfElementType<CopyPasteItemSubmodel>())
                        {
                            this.context?.MessageBoxFlyoutShow(
                                    "No (valid) information for Submodels in copy/paste buffer.",
                                    "Copy & Paste",
                                    AnyUiMessageBoxButton.OK, AnyUiMessageBoxImage.Information);
                            return new AnyUiLambdaActionNone();
                        }

                        // user feedback
                        Log.Singleton.Info($"Pasting {cpb.Items.Count} Submodels from paste buffer");

                        // loop over items
                        object nextBusObj = null;
                        List<CopyPasteItemBase> seq = cpb.Items;
                        if (buttonNdx == 3)
                            seq.Reverse();
                        foreach (var it in seq)
                        {
                            // access
                            var item = it as CopyPasteItemSubmodel;
                            if (item?.sm == null || (!cpb.Duplicate && item?.parentContainer == null))
                            {
                                Log.Singleton.Error("When pasting AAS elements, an element was invalid.");
                                continue;
                            }

                            // determine, what to clone
                            object src = item.sm;
                            if (typeof(T) == typeof(Reference))
                                src = item.smref;

                            if (src == null)
                            {
                                Log.Singleton.Error("When pasting AAS elements, an element was not determined.");
                                continue;
                            }

                            // check for equality
                            bool foundEqual = false;
                            if (checkEquality != null && src is T x)
                                foreach (var p in parentContainer)
                                    if (checkEquality(p, x))
                                        foundEqual = true;
                            if (foundEqual)
                            {
                                Log.Singleton.Error("When pasting AAS elements, an element was found to be " +
                                    "already existing.");
                                continue;
                            }

                            // apply
                            object entity2 = cloneEntity((T)src);
                            nextBusObj = entity2;

                            // emit event
                            this.AddDiaryEntry(entity2 as IReferable,
                                new DiaryEntryStructChange(StructuralChangeReason.Create));

                            // different cases
                            if (buttonNdx == 2)
                                this.AddElementInListBefore<T>(parentContainer, (T)entity2, entity);
                            if (buttonNdx == 3)
                                this.AddElementInListAfter<T>(parentContainer, (T)entity2, entity);

                            // extra action
                            extraAction?.Invoke(it);

                            // may delete original
                            if (!cpb.Duplicate)
                            {
                                this.DeleteElementInList<T>(
                                        item.parentContainer as List<T>, (T)src, null);

                                this.AddDiaryEntry(src as IReferable,
                                    new DiaryEntryStructChange(StructuralChangeReason.Delete));
                            }
                        }

                        // the buffer is tainted
                        cpb.Clear();

                        // try to focus
                        return new AnyUiLambdaActionRedrawAllElements(
                            nextFocus: nextBusObj, isExpanded: true);
                    }

                    if (buttonNdx == 4)
                    {
                        // which buffer?
                        var cbdata = context?.ClipboardGet();
                        var cpb = cpbInternal.CheckIfUseExternalCopyPasteBuffer(cbdata);

                        // content?
                        if (!cpb.ContentAvailable)
                        {
                            this.context?.MessageBoxFlyoutShow(
                                "No sufficient infomation in internal paste buffer or external clipboard.",
                                "Copy & Paste",
                                AnyUiMessageBoxButton.OK, AnyUiMessageBoxImage.Error);
                            return new AnyUiLambdaActionNone();
                        }

                        // pasting above/ below means: Submodels
                        if (!cpb.Items.AllOfElementType<CopyPasteItemSME>())
                        {
                            this.context?.MessageBoxFlyoutShow(
                                    "No (valid) information for SubmodelsElements in copy/paste buffer.",
                                    "Copy & Paste",
                                    AnyUiMessageBoxButton.OK, AnyUiMessageBoxImage.Information);
                            return new AnyUiLambdaActionNone();
                        }

                        // user feedback
                        Log.Singleton.Info($"Pasting {cpb.Items.Count} SubmodelElements from paste buffer");

                        // loop over items
                        object nextBusObj = null;
                        foreach (var it in cpb.Items)
                        {
                            // access
                            var item = it as CopyPasteItemSME;
                            if (item?.sme == null || item?.wrapper == null
                                || (!cpb.Duplicate && item?.parentContainer == null))
                            {
                                Log.Singleton.Error("When pasting SubmodelElements, an element was invalid.");
                                continue;
                            }

                            // apply
                            var smw2 = item.sme.Copy();
                            nextBusObj = smw2;

                            //if (sm is IEnumerateChildren smeec)
                            //    smeec.AddChild(smw2);
                            
                            sm.AddChild(smw2);

                            // emit event
                            this.AddDiaryEntry(item.sme, new DiaryEntryStructChange(StructuralChangeReason.Create));

                            // may delete original
                            if (!cpb.Duplicate)
                            {
                                DispDeleteCopyPasteItem(item);

                                this.AddDiaryEntry(item.sme,
                                    new DiaryEntryStructChange(StructuralChangeReason.Delete));
                            }
                        }

                        // the buffer is tainted
                        cpb.Clear();

                        // try to focus
                        return new AnyUiLambdaActionRedrawAllElements(
                            nextFocus: nextBusObj, isExpanded: true);
                    }

                    return new AnyUiLambdaActionNone();
                });
        }

        public void DispPlainIdentifiableCutCopyPasteHelper<T>(
            AnyUiPanel stack,
            ModifyRepo repo,
            CopyPasteBuffer cpbInternal,
            List<T> parentContainer,
            T entity,
            Func<T, T> cloneEntity,
            string label = "Buffer:",
            Func<CopyPasteBuffer, bool> checkPasteInfo = null,
            Func<CopyPasteItemBase, bool, object> doPasteInto = null)
                where T : IIdentifiable/*, new()*/ //TODO:jtikekar Test
        {
            // access
            if (parentContainer == null || cpbInternal == null || entity == null || cloneEntity == null)
                return;

            // use an action
            this.AddAction(
                stack, label,
                new[] { "Cut", "Copy", "Paste above", "Paste below", "Paste into" }, repo,
                (buttonNdx) =>
                {
                    if (buttonNdx == 0 || buttonNdx == 1)
                    {
                        // store info
                        cpbInternal.Clear();
                        cpbInternal.Valid = true;
                        cpbInternal.Duplicate = buttonNdx == 1;
                        cpbInternal.Items = new ListOfCopyPasteItem(
                            new CopyPasteItemIdentifiable(parentContainer, entity));
                        cpbInternal.CopyToClipboard(context, cpbInternal.Watermark);

                        // user feedback
                        Log.Singleton.Info(
                            StoredPrint.Color.Blue,
                            "Stored {0} '{1}' to internal buffer.{2}",
                            "" + entity.GetSelfDescription().AasElementName,
                            "" + entity.IdShort,
                            cpbInternal.Duplicate
                                ? " Paste will duplicate."
                                : " Paste will cut at original position.");
                    }

                    if (buttonNdx == 2 || buttonNdx == 3)
                    {
                        // which buffer?
                        var cbdata = context?.ClipboardGet();
                        var cpb = cpbInternal.CheckIfUseExternalCopyPasteBuffer(cbdata);
                        if (!cpb.ContentAvailable)
                        {
                            this.context?.MessageBoxFlyoutShow(
                                "No sufficient infomation in internal paste buffer or external clipboard.",
                                "Copy & Paste",
                                AnyUiMessageBoxButton.OK, AnyUiMessageBoxImage.Error);
                            return new AnyUiLambdaActionNone();
                        }

                        // pasting above/ below means: Submodels
                        if (!cpb.Items.AllOfElementType<CopyPasteItemIdentifiable>())
                        {
                            this.context?.MessageBoxFlyoutShow(
                                    "No (valid) information for Identifiables in copy/paste buffer.",
                                    "Copy & Paste",
                                    AnyUiMessageBoxButton.OK, AnyUiMessageBoxImage.Information);
                            return new AnyUiLambdaActionNone();
                        }

                        // user feedback
                        Log.Singleton.Info($"Pasting {cpb.Items.Count} Identifiables from paste buffer");

                        // loop over items
                        object nextBusObj = null;
                        foreach (var it in cpb.Items)
                        {
                            // access
                            var item = it as CopyPasteItemIdentifiable;
                            if (item?.entity == null || (!cpb.Duplicate && item?.parentContainer == null))
                            {
                                Log.Singleton.Error("When pasting Identifiables, an element was invalid.");
                                continue;
                            }

                            // apply
                            object entity2 = cloneEntity((T)item.entity);
                            nextBusObj = entity2;

                            // make this pseudo-unique
                            this.MakeNewIdentifiableUnique((T)entity2);

                            // different cases
                            int ndx = -1;
                            if (buttonNdx == 2)
                                ndx = this.AddElementInListBefore<T>(parentContainer, (T)entity2, entity);
                            if (buttonNdx == 3)
                                ndx = this.AddElementInListAfter<T>(parentContainer, (T)entity2, entity);

                            // Identifiable: just state as newly created
                            this.AddDiaryEntry((T)entity2, new DiaryEntryStructChange(
                                AasxIntegrationBase.AdminShellEvents.StructuralChangeReason.Create,
                                createAtIndex: ndx));

                            // may delete original
                            if (!cpb.Duplicate)
                            {
                                this.DeleteElementInList<T>(
                                        item.parentContainer as List<T>, (T)item.entity, null);

                                this.AddDiaryEntry((T)entity, new DiaryEntryStructChange(
                                    AasxIntegrationBase.AdminShellEvents.StructuralChangeReason.Delete));
                            }
                        }

                        // the buffer is tainted
                        cpb.Clear();

                        // try to focus
                        return new AnyUiLambdaActionRedrawAllElements(
                            nextFocus: nextBusObj, isExpanded: true);
                    }

                    if (buttonNdx == 4)
                    {
                        // which buffer?
                        var cbdata = context?.ClipboardGet();
                        var cpb = cpbInternal.CheckIfUseExternalCopyPasteBuffer(cbdata);
                        if (cpb == null)
                        {
                            Log.Singleton.Error("Internal error in CheckIfUseExternalCopyPasteBuffer()");
                            return new AnyUiLambdaActionNone();
                        }

                        if (!cpb.ContentAvailable)
                        {
                            this.context?.MessageBoxFlyoutShow(
                                "No sufficient infomation in internal paste buffer or external clipboard.",
                                "Copy & Paste",
                                AnyUiMessageBoxButton.OK, AnyUiMessageBoxImage.Error);
                            return new AnyUiLambdaActionNone();
                        }

                        // pasting above/ below means: Submodels
                        if (checkPasteInfo != null && !checkPasteInfo(cpb))
                        {
                            this.context?.MessageBoxFlyoutShow(
                                    "No (valid) information for Identifiables in copy/paste buffer.",
                                    "Copy & Paste",
                                    AnyUiMessageBoxButton.OK, AnyUiMessageBoxImage.Information);
                            return new AnyUiLambdaActionNone();
                        }

                        // user feedback
                        Log.Singleton.Info($"Pasting {cpb.Items.Count} AAS elements from paste buffer");

                        // loop over items
                        object nextBusObj = null;
                        foreach (var it in cpb.Items)
                        {
                            // try
                            var obj = doPasteInto?.Invoke(it, !cpb.Duplicate);
                            if (obj == null)
                            {
                                Log.Singleton.Error("When pasting AAS elements, an element was invalid.");
                            }
                            else
                                nextBusObj = obj;
                        }

                        // the buffer is tainted
                        cpb.Clear();

                        // try to focus
                        return new AnyUiLambdaActionRedrawAllElements(
                            nextFocus: nextBusObj, isExpanded: true);
                    }

                    return new AnyUiLambdaActionNone();
                });
        }

        // resharper disable once UnusedTypeParameter
        public void DispPlainListOfIdentifiablePasteHelper<T>(
            AnyUiPanel stack,
            ModifyRepo repo,
            CopyPasteBuffer cpbInternal,
            string label = "Buffer:",
            Func<CopyPasteItemBase, bool, object> lambdaPasteInto = null)
                where T : IIdentifiable/*, new()*/   //TODO: jtikekar test
        {
            // access
            if (cpbInternal == null || lambdaPasteInto == null)
                return;

            // use an action
            this.AddAction(
                stack, label,
                new[] { "Paste into" }, repo,
                (buttonNdx) =>
                {
                    if (buttonNdx == 0)
                    {
                        // which buffer
                        var cbdata = context?.ClipboardGet();
                        var cpb = cpbInternal.CheckIfUseExternalCopyPasteBuffer(cbdata);
                        if (!cpb.ContentAvailable)
                        {
                            this.context?.MessageBoxFlyoutShow(
                                "No sufficient infomation in internal paste buffer or external clipboard.",
                                "Copy & Paste",
                                AnyUiMessageBoxButton.OK, AnyUiMessageBoxImage.Error);
                            return new AnyUiLambdaActionNone();
                        }

                        // user feedback
                        Log.Singleton.Info($"Pasting {cpb.Items.Count} AAS elements from paste buffer");

                        // loop over items
                        object nextBusObj = null;
                        var doDelete = !cpb.Duplicate && !cpb.ExternalSource;
                        foreach (var it in cpb.Items)
                        {
                            // try
                            var obj = lambdaPasteInto(it, doDelete);
                            if (obj == null)
                            {
                                Log.Singleton.Error("When pasting AAS elements, an element was invalid.");
                            }
                            else
                                nextBusObj = obj;
                        }

                        // the buffer is tainted
                        cpb.Clear();

                        // try to focus
                        return new AnyUiLambdaActionRedrawAllElements(
                            nextFocus: nextBusObj, isExpanded: true);
                    }

                    return new AnyUiLambdaActionNone();
                });
        }
    }
}
