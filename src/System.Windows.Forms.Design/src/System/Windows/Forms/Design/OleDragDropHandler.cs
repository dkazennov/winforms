﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System.Collections;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.ComponentModel.Design.Serialization;
using System.Drawing;
using System.Drawing.Design;
using System.Windows.Forms.Design.Behavior;
using Windows.Win32.System.SystemServices;

namespace System.Windows.Forms.Design;

internal partial class OleDragDropHandler
{
    // This is a bit that we stuff into the DoDragDrop
    // to indicate that the thing that is being dragged should only
    // be allowed to be moved in the current DropTarget (e.g. parent designer).
    // We use this for inherited components that can be modified (e.g. location/size) changed
    // but not removed from their parent.
    //
    protected const int AllowLocalMoveOnly = 0x04000000;

    private readonly SelectionUIHandler selectionHandler;
    private readonly IServiceProvider serviceProvider;
    private readonly IOleDragClient client;

    private bool dragOk;
    private bool forceDrawFrames;
    private bool localDrag;
    private bool localDragInside;
    private Point localDragOffset = Point.Empty;
    private DragDropEffects localDragEffect;
    private object[] dragComps;
    private Point dragBase = Point.Empty;
    private static bool freezePainting;
    private static Dictionary<IDataObject, IComponent> currentDrags;

    public const string CF_CODE = "CF_XMLCODE";
    public const string CF_COMPONENTTYPES = "CF_COMPONENTTYPES";
    public const string CF_TOOLBOXITEM = "CF_NESTEDTOOLBOXITEM";

    public OleDragDropHandler(SelectionUIHandler selectionHandler, IServiceProvider serviceProvider, IOleDragClient client)
    {
        this.serviceProvider = serviceProvider;
        this.selectionHandler = selectionHandler;
        this.client = client;
    }

    public static string DataFormat
    {
        get
        {
            return CF_CODE;
        }
    }

    public static string ExtraInfoFormat
    {
        get
        {
            return CF_COMPONENTTYPES;
        }
    }

    public static string NestedToolboxItemFormat
    {
        get
        {
            return CF_TOOLBOXITEM;
        }
    }

    private static IComponent GetDragOwnerComponent(IDataObject data)
    {
        return currentDrags is null || !currentDrags.TryGetValue(data, out IComponent value) ? null : value;
    }

    private static void AddCurrentDrag(IDataObject data, IComponent component)
    {
        currentDrags ??= new();
        currentDrags[data] = component;
    }

    private static void RemoveCurrentDrag(IDataObject data)
    {
        currentDrags.Remove(data);
    }

    internal IOleDragClient Destination
    {
        get
        {
            return client;
        }
    }

    protected virtual bool CanDropDataObject(IDataObject dataObj)
    {
        if (dataObj is not null)
        {
            if (dataObj is ComponentDataObjectWrapper)
            {
                object[] dragObjs = GetDraggingObjects(dataObj, true);
                if (dragObjs is null)
                {
                    return false;
                }

                bool dropOk = true;
                for (int i = 0; dropOk && i < dragObjs.Length; i++)
                {
                    dropOk = dropOk && (dragObjs[i] is IComponent) && client.IsDropOk((IComponent)dragObjs[i]);
                }

                return dropOk;
            }

            try
            {
                object serializationData = dataObj.GetData(DataFormat, false);

                if (serializationData is null)
                {
                    return false;
                }

                IDesignerSerializationService ds = (IDesignerSerializationService)GetService(typeof(IDesignerSerializationService));
                if (ds is null)
                {
                    return false;
                }

                ICollection objects = ds.Deserialize(serializationData);
                if (objects.Count > 0)
                {
                    bool dropOk = true;

                    foreach (object o in objects)
                    {
                        if (!(o is IComponent))
                        {
                            continue;
                        }

                        dropOk = dropOk && client.IsDropOk((IComponent)o);
                        if (!dropOk)
                            break;
                    }

                    return dropOk;
                }
            }
            catch (Exception ex) when (!ex.IsCriticalException())
            {
                // We return false on any exception.
            }
        }

        return false;
    }

    public bool Dragging
    {
        get
        {
            return localDrag;
        }
    }

    public static bool FreezePainting
    {
        get
        {
            return freezePainting;
        }
    }

    /// <summary>
    ///  This is the worker method of all CreateTool methods.  It is the only one
    ///  that can be overridden.
    /// </summary>
    public IComponent[] CreateTool(ToolboxItem tool, Control parent, int x, int y, int width, int height, bool hasLocation, bool hasSize)
    {
        return CreateTool(tool, parent, x, y, width, height, hasLocation, hasSize, null);
    }

    public IComponent[] CreateTool(ToolboxItem tool, Control parent, int x, int y, int width, int height, bool hasLocation, bool hasSize, ToolboxSnapDragDropEventArgs e)
    {
        // Services we will need
        //
        IToolboxService toolboxSvc = (IToolboxService)GetService(typeof(IToolboxService));
        ISelectionService selSvc = (ISelectionService)GetService(typeof(ISelectionService));
        IDesignerHost host = (IDesignerHost)GetService(typeof(IDesignerHost));
        IComponent[] comps = Array.Empty<IComponent>();

        Cursor oldCursor = Cursor.Current;
        Cursor.Current = Cursors.WaitCursor;
        DesignerTransaction trans = null;

        try
        {
            try
            {
                if (host is not null)
                    trans = host.CreateTransaction(string.Format(SR.DesignerBatchCreateTool, tool));
            }
            catch (CheckoutException cxe)
            {
                if (cxe == CheckoutException.Canceled)
                    return comps;

                throw;
            }

            try
            {
                try
                {
                    // First check if we are currently in localization mode (i.e., language is non-default).
                    // If so, we should not permit addition of new components. This is an intentional
                    // change from Everett - see VSWhidbey #292249.
                    if (host is not null && CurrentlyLocalizing(host.RootComponent))
                    {
                        IUIService uiService = (IUIService)GetService(typeof(IUIService));
                        uiService?.ShowMessage(SR.LocalizingCannotAdd);

                        comps = Array.Empty<IComponent>();
                        return comps;
                    }

                    // Create a dictionary of default values that the designer can
                    // use to initialize a control with.
                    Hashtable defaultValues = new Hashtable();
                    if (parent is not null)
                        defaultValues["Parent"] = parent;

                    // adjust the location if we are in a mirrored parent. That is because the origin
                    // will then be in the upper right rather than upper left.
                    if (parent is not null && parent.IsMirrored)
                    {
                        x += width;
                    }

                    if (hasLocation)
                        defaultValues["Location"] = new Point(x, y);
                    if (hasSize)
                        defaultValues["Size"] = new Size(width, height);
                    //store off extra behavior drag/drop information
                    if (e is not null)
                        defaultValues["ToolboxSnapDragDropEventArgs"] = e;

                    comps = tool.CreateComponents(host, defaultValues);
                }
                catch (CheckoutException checkoutEx)
                {
                    if (checkoutEx == CheckoutException.Canceled)
                    {
                        comps = Array.Empty<IComponent>();
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (ArgumentException argumentEx)
                {
                    IUIService uiService = (IUIService)GetService(typeof(IUIService));
                    uiService?.ShowError(argumentEx);
                }
                catch (Exception ex)
                {
                    IUIService uiService = (IUIService)GetService(typeof(IUIService));

                    string exceptionMessage = string.Empty;
                    if (ex.InnerException is not null)
                    {
                        exceptionMessage = ex.InnerException.ToString();
                    }

                    if (string.IsNullOrEmpty(exceptionMessage))
                    {
                        exceptionMessage = ex.ToString();
                    }

                    if (ex is InvalidOperationException)
                    {
                        exceptionMessage = ex.Message;
                    }

                    if (uiService is not null)
                    {
                        uiService.ShowError(ex, string.Format(SR.FailedToCreateComponent, tool.DisplayName, exceptionMessage));
                    }
                    else
                    {
                        throw;
                    }
                }

                comps ??= Array.Empty<IComponent>();
            }
            finally
            {
                if (toolboxSvc is not null && tool.Equals(toolboxSvc.GetSelectedToolboxItem(host)))
                {
                    toolboxSvc.SelectedToolboxItemUsed();
                }
            }
        }
        finally
        {
            trans?.Commit();

            Cursor.Current = oldCursor;
        }

        // Finally, select the newly created components.
        //
        if (selSvc is not null && comps.Length > 0)
        {
            host?.Activate();

            ArrayList selectComps = new ArrayList(comps);

            for (int i = 0; i < comps.Length; i++)
            {
                if (!TypeDescriptor.GetAttributes(comps[i]).Contains(DesignTimeVisibleAttribute.Yes))
                {
                    selectComps.Remove(comps[i]);
                }
            }

            selSvc.SetSelectedComponents(selectComps.ToArray(), SelectionTypes.Replace);
        }

        return comps;
    }

    /// <summary>
    ///  Determines whether we are currently in localization mode - i.e., language is not (Default).
    /// </summary>
    private static bool CurrentlyLocalizing(IComponent rootComponent)
    {
        if (rootComponent is not null)
        {
            PropertyDescriptor prop = TypeDescriptor.GetProperties(rootComponent)["Language"];

            if (prop is not null && prop.PropertyType == typeof(Globalization.CultureInfo))
            {
                Globalization.CultureInfo ci = (Globalization.CultureInfo)prop.GetValue(rootComponent);
                if (!ci.Equals(Globalization.CultureInfo.InvariantCulture))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void DisableDragDropChildren(ICollection controls, ArrayList allowDropCache)
    {
        foreach (Control c in controls)
        {
            if (c is not null)
            {
                if (c.AllowDrop)
                {
                    allowDropCache.Add(c);
                    c.AllowDrop = false;
                }

                if (c.HasChildren)
                {
                    DisableDragDropChildren(c.Controls, allowDropCache);
                }
            }
        }
    }

    private Point DrawDragFrames(object[] comps,
                                 Point oldOffset, DragDropEffects oldEffect,
                                 Point newOffset, DragDropEffects newEffect,
                                 bool drawAtNewOffset)
    {
        Control comp;
        Control parentControl = client.GetDesignerControl();

        if (selectionHandler is null)
        {
            Debug.Fail("selectionHandler should not be null");
            return Point.Empty;
        }

        if (comps is null)
        {
            return Point.Empty;
        }

        for (int i = 0; i < comps.Length; i++)
        {
            comp = client.GetControlForComponent(comps[i]);

            Color backColor = SystemColors.Control;
            try
            {
                backColor = comp.BackColor;
            }
            catch (Exception ex) when (!ex.IsCriticalException())
            {
            }

            // If we are moving, we must make sure that the location property of the component
            // is not read only.  Otherwise, we can't move the thing.
            bool readOnlyLocation = true;

            PropertyDescriptor loc = TypeDescriptor.GetProperties(comps[i])["Location"];
            if (loc is not null)
            {
                readOnlyLocation = loc.IsReadOnly;
            }

            Rectangle newRect;
            // first, undraw the old rect
            if (!oldOffset.IsEmpty)
            {
                if ((oldEffect & DragDropEffects.Move) == 0 ||
                    !readOnlyLocation)
                {
                    newRect = comp.Bounds;

                    if (drawAtNewOffset)
                    {
                        newRect.X = oldOffset.X;
                        newRect.Y = oldOffset.Y;
                    }
                    else
                    {
                        newRect.Offset(oldOffset.X, oldOffset.Y);
                    }

                    newRect = selectionHandler.GetUpdatedRect(comp.Bounds, newRect, false);
                    DrawReversibleFrame((HWND)parentControl.Handle, newRect, backColor);
                }
            }

            if (!newOffset.IsEmpty)
            {
                if ((oldEffect & DragDropEffects.Move) == 0 ||
                    !readOnlyLocation)
                {
                    newRect = comp.Bounds;
                    if (drawAtNewOffset)
                    {
                        newRect.X = newOffset.X;
                        newRect.Y = newOffset.Y;
                    }
                    else
                    {
                        newRect.Offset(newOffset.X, newOffset.Y);
                    }

                    newRect = selectionHandler.GetUpdatedRect(comp.Bounds, newRect, false);
                    DrawReversibleFrame((HWND)parentControl.Handle, newRect, backColor);
                }
            }
        }

        return newOffset;
    }

    private static void DrawReversibleFrame(HWND handle, Rectangle rectangle, Color backColor)
    {
        //Bug # 71547 <subhag> to make drag rect visible if any the dimensions of the control are 0
        if (rectangle.Width == 0)
            rectangle.Width = 5;
        if (rectangle.Height == 0)
            rectangle.Height = 5;

        // Copy of ControlPaint.DrawReversibleFrame, see VSWhidbey 581670
        // If ControlPaint ever gets overloaded, we should replace the code below by calling it:
        // ControlPaint.DrawReversibleFrame(handle, rectangle, backColor, FrameStyle.Thick);

        // ------ Duplicate code----------------------------------------------------------
        R2_MODE rop2;
        Color graphicsColor;

        if (backColor.GetBrightness() < .5)
        {
            rop2 = R2_MODE.R2_NOTXORPEN;
            graphicsColor = Color.White;
        }
        else
        {
            rop2 = R2_MODE.R2_XORPEN;
            graphicsColor = Color.Black;
        }

        using GetDcScope dc = new(handle);
        using PInvoke.ObjectScope pen =
            new(PInvoke.CreatePen(PEN_STYLE.PS_SOLID, cWidth: 2, (COLORREF)(uint)ColorTranslator.ToWin32(backColor)));

        using PInvoke.SetRop2Scope rop2Scope = new(dc, rop2);
        using PInvoke.SelectObjectScope brushSelection = new(dc, PInvoke.GetStockObject(GET_STOCK_OBJECT_FLAGS.NULL_BRUSH));
        using PInvoke.SelectObjectScope penSelection = new(dc, pen);

        PInvoke.SetBkColor(dc, (COLORREF)(uint)ColorTranslator.ToWin32(graphicsColor));
        PInvoke.Rectangle(dc, rectangle.X, rectangle.Y, rectangle.Right, rectangle.Bottom);
        // ------ Duplicate code----------------------------------------------------------
    }

    public unsafe bool DoBeginDrag(object[] components, SelectionRules rules, int initialX, int initialY)
    {
        // if we're in a sizing operation, or the mouse isn't down, don't do this!
        if ((rules & SelectionRules.AllSizeable) != SelectionRules.None || Control.MouseButtons == MouseButtons.None)
        {
            return true;
        }

        Control c = client.GetDesignerControl();

        localDrag = true;
        localDragInside = false;

        dragComps = components;
        dragBase = new Point(initialX, initialY);
        localDragOffset = Point.Empty;
        c.PointToClient(new Point(initialX, initialY));

        DragDropEffects allowedEffects = DragDropEffects.Copy | DragDropEffects.None | DragDropEffects.Move;

        // check to see if any of the components are inherited. if so, don't allow them to be moved.
        // We replace DragDropEffects.Move with a local bit called AllowLocalMoveOnly which means it
        // can be moved around on the current dropsource/target, but not to another target.  Since only
        // we understand this bit, other drop targets will not allow the move to occur
        //
        for (int i = 0; i < components.Length; i++)
        {
            InheritanceAttribute attr = (InheritanceAttribute)TypeDescriptor.GetAttributes(components[i])[typeof(InheritanceAttribute)];

            if (!attr.Equals(InheritanceAttribute.NotInherited) && !attr.Equals(InheritanceAttribute.InheritedReadOnly))
            {
                allowedEffects &= ~DragDropEffects.Move;
                allowedEffects |= (DragDropEffects)AllowLocalMoveOnly;
            }
        }

        DataObject data = new ComponentDataObjectWrapper(new ComponentDataObject(client, serviceProvider, components, initialX, initialY));

        // We make sure we're painted before we start the drag.  Then, we disable window painting to
        // ensure that the drag can proceed without leaving artifacts lying around.  We should be calling LockWindowUpdate,
        // but that causes a horrible flashing because GDI+ uses direct draw.
        MSG msg = default;
        while (PInvoke.PeekMessage(&msg, HWND.Null, (uint)PInvoke.WM_PAINT, (uint)PInvoke.WM_PAINT, PEEK_MESSAGE_REMOVE_TYPE.PM_REMOVE))
        {
            PInvoke.TranslateMessage(msg);
            PInvoke.DispatchMessage(&msg);
        }

        // don't do any new painting...
        bool oldFreezePainting = freezePainting;

        // ASURT 90345 -- this causes some subtle bugs, so i'm turning it off to see if we really need it, and if we do
        // if we can find a better way.
        //
        //freezePainting = true;

        AddCurrentDrag(data, client.Component);

        // Walk through all the children recursively and disable drag-drop
        // for each of them. This way, we will not accidentally try to drop
        // ourselves into our own children.
        //
        ArrayList allowDropChanged = new ArrayList();
        foreach (object comp in components)
        {
            Control ctl = comp as Control;
            if (ctl is not null && ctl.HasChildren)
            {
                DisableDragDropChildren(ctl.Controls, allowDropChanged);
            }
        }

        DragDropEffects effect = DragDropEffects.None;
        IDesignerHost host = GetService(typeof(IDesignerHost)) as IDesignerHost;
        DesignerTransaction trans = null;
        if (host is not null)
        {
            trans = host.CreateTransaction(string.Format(SR.DragDropDragComponents, components.Length));
        }

        try
        {
            effect = c.DoDragDrop(data, allowedEffects);
            trans?.Commit();
        }
        finally
        {
            RemoveCurrentDrag(data);

            // Reset the AllowDrop for the components being dragged.
            //
            foreach (Control ctl in allowDropChanged)
            {
                ctl.AllowDrop = true;
            }

            freezePainting = oldFreezePainting;

            if (trans is not null)
            {
                ((IDisposable)trans).Dispose();
            }
        }

        bool isMove = (effect & DragDropEffects.Move) != 0 || ((int)effect & AllowLocalMoveOnly) != 0;

        // since the EndDrag will clear this
        bool isLocalMove = isMove && localDragInside;

        ISelectionUIService selectionUISvc = (ISelectionUIService)GetService(typeof(ISelectionUIService));
        Debug.Assert(selectionUISvc is not null, "Unable to get selection ui service when adding child control");

        if (selectionUISvc is not null)
        {
            // We must check to ensure that UI service is still in drag mode.  It is
            // possible that the user hit escape, which will cancel drag mode.
            //
            if (selectionUISvc.Dragging)
            {
                // cancel the drag if we aren't doing a local move
                selectionUISvc.EndDrag(!isLocalMove);
            }
        }

        if (!localDragOffset.IsEmpty && effect != DragDropEffects.None)
        {
            DrawDragFrames(dragComps, localDragOffset, localDragEffect,
                           Point.Empty, DragDropEffects.None, false);
        }

        localDragOffset = Point.Empty;
        dragComps = null;
        localDrag = localDragInside = false;
        dragBase = Point.Empty;

        /*if (!isLocalMove && isMove){
            IDesignerHost host = (IDesignerHost)GetService(typeof(IDesignerHost));
            IUndoService  undoService = (IUndoService)GetService(typeof(IUndoService));
            IActionUnit unit = null;

            if (host != null) {
                DesignerTransaction trans = host.CreateTransaction("Drag/drop");
                try{
                    // delete the components
                    try{
                        for (int i = 0; i < components.Length; i++){
                           if (components[i] is IComponent){
                              if (undoService != null){
                                  unit = new CreateControlActionUnit(host, (IComponent)components[i], true);
                              }
                              host.DestroyComponent((IComponent)components[i]);
                              if (undoService != null){
                                   undoService.AddAction(unit, false);
                              }
                           }
                        }
                    }catch(CheckoutException ex){
                        if (ex != CheckoutException.Canceled){
                            throw ex;
                        }
                    }
                }
                finally{
                    trans.Commit();
                }
            }
        }*/

        return false;
    }

    public void DoEndDrag(object[] components, bool cancel)
    {
        dragComps = null;
        localDrag = false;
        localDragInside = false;
    }

    public void DoOleDragDrop(DragEventArgs de)
    {
        // ASURT 43757: By the time we come here, it means that the user completed the drag-drop and
        // we compute the new location/size of the controls if needed and set the property values.
        // We have to stop freezePainting right here, so that controls can get a chance to validate
        // their new rects.
        //
        freezePainting = false;

        if (selectionHandler is null)
        {
            Debug.Fail("selectionHandler should not be null");
            de.Effect = DragDropEffects.None;
            return;
        }

        // make sure we've actually moved
        if ((localDrag && de.X == dragBase.X && de.Y == dragBase.Y) ||
            de.AllowedEffect == DragDropEffects.None ||
            (!localDrag && !dragOk))
        {
            de.Effect = DragDropEffects.None;
            return;
        }

        bool localMoveOnly = ((int)de.AllowedEffect & AllowLocalMoveOnly) != 0 && localDragInside;

        // if we are dragging inside the local dropsource/target, and and AllowLocalMoveOnly flag is set,
        // we just consider this a normal move.
        //
        bool moveAllowed = (de.AllowedEffect & DragDropEffects.Move) != DragDropEffects.None || localMoveOnly;
        bool copyAllowed = (de.AllowedEffect & DragDropEffects.Copy) != DragDropEffects.None;

        if ((de.Effect & DragDropEffects.Move) != 0 && !moveAllowed)
        {
            // Try copy instead?
            de.Effect = DragDropEffects.Copy;
        }

        // make sure the copy is allowed
        if ((de.Effect & DragDropEffects.Copy) != 0 && !copyAllowed)
        {
            // if copy isn't allowed, don't do anything

            de.Effect = DragDropEffects.None;
            return;
        }

        if (localMoveOnly && (de.Effect & DragDropEffects.Move) != 0)
        {
            de.Effect |= (DragDropEffects)AllowLocalMoveOnly | DragDropEffects.Move;
        }
        else if ((de.Effect & DragDropEffects.Copy) != 0)
        {
            de.Effect = DragDropEffects.Copy;
        }

        if (forceDrawFrames || localDragInside)
        {
            // undraw the drag rect
            localDragOffset = DrawDragFrames(dragComps, localDragOffset, localDragEffect,
                                             Point.Empty, DragDropEffects.None, forceDrawFrames);
            forceDrawFrames = false;
        }

        Cursor oldCursor = Cursor.Current;

        try
        {
            Cursor.Current = Cursors.WaitCursor;

            if (dragOk || (localDragInside && de.Effect == DragDropEffects.Copy))
            {
                // add em to this parent.
                IDesignerHost host = (IDesignerHost)GetService(typeof(IDesignerHost));
                IContainer container = host.RootComponent.Site.Container;

                object[] components;
                IDataObject dataObj = de.Data;
                bool updateLocation = false;

                if (dataObj is ComponentDataObjectWrapper)
                {
                    dataObj = ((ComponentDataObjectWrapper)dataObj).InnerData;
                    ComponentDataObject cdo = (ComponentDataObject)dataObj;

                    // if we're moving ot a different container, do a full serialization
                    // to make sure we pick up design time props, etc.
                    //
                    IComponent dragOwner = GetDragOwnerComponent(de.Data);
                    bool newContainer = dragOwner is null || client.Component is null || dragOwner.Site.Container != client.Component.Site.Container;
                    bool collapseChildren = false;
                    if (de.Effect == DragDropEffects.Copy || newContainer)
                    {
                        // this causes new elements to be created
                        //
                        cdo.Deserialize(serviceProvider, (de.Effect & DragDropEffects.Copy) == 0);
                    }
                    else
                    {
                        collapseChildren = true;
                    }

                    updateLocation = true;
                    components = cdo.Components;

                    if (collapseChildren)
                    {
                        components = GetTopLevelComponents(components);
                    }
                }
                else
                {
                    object serializationData = dataObj.GetData(DataFormat, true);

                    if (serializationData is null)
                    {
                        Debug.Fail("data object didn't return any data, so how did we allow the drop?");
                        components = Array.Empty<IComponent>();
                    }
                    else
                    {
                        dataObj = new ComponentDataObject(client, serviceProvider, serializationData);
                        components = ((ComponentDataObject)dataObj).Components;
                        updateLocation = true;
                    }
                }

                // now we need to offset the components locations from the drop mouse
                // point to the parent, since their current locations are relative
                // the mouse pointer
                if (components is not null && components.Length > 0)
                {
                    Debug.Assert(container is not null, "Didn't get a container from the site!");
                    string name;
                    IComponent comp = null;

                    DesignerTransaction trans = null;

                    try
                    {
                        trans = host.CreateTransaction(SR.DragDropDropComponents);
                        if (!localDrag)
                        {
                            host.Activate();
                        }

                        ArrayList selectComps = new ArrayList();

                        for (int i = 0; i < components.Length; i++)
                        {
                            comp = components[i] as IComponent;

                            if (comp is null)
                            {
                                comp = null;
                                continue;
                            }

                            try
                            {
                                name = comp.Site?.Name;

                                Control oldDesignerControl = null;
                                if (updateLocation)
                                {
                                    oldDesignerControl = client.GetDesignerControl();
                                    PInvoke.SendMessage(oldDesignerControl, PInvoke.WM_SETREDRAW, (WPARAM)(BOOL)false);
                                }

                                Point dropPt = client.GetDesignerControl().PointToClient(new Point(de.X, de.Y));

                                // First check if the component we are dropping have a TrayLocation, and if so, use it
                                PropertyDescriptor loc = TypeDescriptor.GetProperties(comp)["TrayLocation"];
                                // it didn't, so let's check for the regular Location
                                loc ??= TypeDescriptor.GetProperties(comp)["Location"];

                                if (loc is not null && !loc.IsReadOnly)
                                {
                                    Rectangle bounds = default(Rectangle);
                                    Point pt = (Point)loc.GetValue(comp);
                                    bounds.X = dropPt.X + pt.X;
                                    bounds.Y = dropPt.Y + pt.Y;
                                    bounds = selectionHandler.GetUpdatedRect(Rectangle.Empty, bounds, false);
                                }

                                if (!client.AddComponent(comp, name, false))
                                {
                                    // this means that we just moved the control
                                    // around in the same designer.

                                    de.Effect = DragDropEffects.None;
                                }
                                else
                                {
                                    // make sure the component was added to this client
                                    if (client.GetControlForComponent(comp) is null)
                                    {
                                        updateLocation = false;
                                    }
                                }

                                if (updateLocation)
                                {
                                    ParentControlDesigner parentDesigner = client as ParentControlDesigner;
                                    if (parentDesigner is not null)
                                    {
                                        Control c = client.GetControlForComponent(comp);
                                        dropPt = parentDesigner.GetSnappedPoint(c.Location);
                                        c.Location = dropPt;
                                    }
                                }

                                if (oldDesignerControl is not null)
                                {
                                    //((ComponentDataObject)dataObj).ShowControls();
                                    PInvoke.SendMessage(oldDesignerControl, PInvoke.WM_SETREDRAW, (WPARAM)(BOOL)true);
                                    oldDesignerControl.Invalidate(true);
                                }

                                if (TypeDescriptor.GetAttributes(comp).Contains(DesignTimeVisibleAttribute.Yes))
                                {
                                    selectComps.Add(comp);
                                }
                            }
                            catch (CheckoutException ceex)
                            {
                                if (ceex == CheckoutException.Canceled)
                                {
                                    break;
                                }

                                throw;
                            }
                        }

                        host?.Activate();

                        // select the newly added components
                        ISelectionService selService = (ISelectionService)GetService(typeof(ISelectionService));

                        selService.SetSelectedComponents((object[])selectComps.ToArray(typeof(IComponent)), SelectionTypes.Replace);
                        localDragInside = false;
                    }
                    finally
                    {
                        trans?.Commit();
                    }
                }
            }

            if (localDragInside)
            {
                ISelectionUIService selectionUISvc = (ISelectionUIService)GetService(typeof(ISelectionUIService));
                Debug.Assert(selectionUISvc is not null, "Unable to get selection ui service when adding child control");

                if (selectionUISvc is not null)
                {
                    // We must check to ensure that UI service is still in drag mode.  It is
                    // possible that the user hit escape, which will cancel drag mode.
                    //
                    if (selectionUISvc.Dragging && moveAllowed)
                    {
                        Rectangle offset = new Rectangle(de.X - dragBase.X, de.Y - dragBase.Y, 0, 0);
                        selectionUISvc.DragMoved(offset);
                    }
                }
            }

            dragOk = false;
        }
        finally
        {
            Cursor.Current = oldCursor;
        }
    }

    public void DoOleDragEnter(DragEventArgs de)
    {
        /*
        this causes focus rects to be drawn, which we don't want to happen.

        Control dragHost = client.GetDesignerControl();

        if (dragHost != null && dragHost.CanSelect) {
            dragHost.Focus();
        }*/

        if (!localDrag && CanDropDataObject(de.Data) && de.AllowedEffect != DragDropEffects.None)
        {
            if (!client.CanModifyComponents)
            {
                return;
            }

            dragOk = true;

            // this means it's not us doing the drag
            if ((de.KeyState & (int)MODIFIERKEYS_FLAGS.MK_CONTROL) != 0 && (de.AllowedEffect & DragDropEffects.Copy) != 0)
            {
                de.Effect = DragDropEffects.Copy;
            }
            else if ((de.AllowedEffect & DragDropEffects.Move) != 0)
            {
                de.Effect = DragDropEffects.Move;
            }
            else
            {
                de.Effect = DragDropEffects.None;
                return;
            }
        }
        else if (localDrag && de.AllowedEffect != DragDropEffects.None)
        {
            localDragInside = true;
            if ((de.KeyState & (int)MODIFIERKEYS_FLAGS.MK_CONTROL) != 0
                && (de.AllowedEffect & DragDropEffects.Copy) != 0
                && client.CanModifyComponents)
            {
                de.Effect = DragDropEffects.Copy;
            }

            bool localMoveOnly = ((int)de.AllowedEffect & AllowLocalMoveOnly) != 0 && localDragInside;

            if (localMoveOnly)
            {
                de.Effect |= (DragDropEffects)AllowLocalMoveOnly;
            }

            if ((de.AllowedEffect & DragDropEffects.Move) != 0)
            {
                de.Effect |= DragDropEffects.Move;
            }
        }
        else
        {
            de.Effect = DragDropEffects.None;
        }
    }

    public void DoOleDragLeave()
    {
        if (localDrag || forceDrawFrames)
        {
            localDragInside = false;
            localDragOffset = DrawDragFrames(dragComps, localDragOffset, localDragEffect,
                                             Point.Empty, DragDropEffects.None, forceDrawFrames);

            if (forceDrawFrames && dragOk)
            {
                dragBase = Point.Empty;
                dragComps = null;
            }

            forceDrawFrames = false;
        }

        dragOk = false;
    }

    public void DoOleDragOver(DragEventArgs de)
    {
        Debug.WriteLineIf(CompModSwitches.DragDrop.TraceInfo, $"\tOleDragDropHandler.OnDragOver: {de}");
        if (!localDrag && !dragOk)
        {
            de.Effect = DragDropEffects.None;
            return;
        }

        bool copy = (de.KeyState & (int)MODIFIERKEYS_FLAGS.MK_CONTROL) != 0
            && (de.AllowedEffect & DragDropEffects.Copy) != 0
            && client.CanModifyComponents;

        // we pretend AllowLocalMoveOnly is a normal move when we are over the originating container.
        //
        bool localMoveOnly = ((int)de.AllowedEffect & AllowLocalMoveOnly) != 0 && localDragInside;
        bool move = (de.AllowedEffect & DragDropEffects.Move) != 0 || localMoveOnly;

        if ((copy || move) && (localDrag || forceDrawFrames))
        {
            Point convertedPoint = client.GetDesignerControl().PointToClient(new Point(de.X, de.Y));

            // draw the shadow rects.
            Point newOffset;
            if (forceDrawFrames)
            {
                newOffset = convertedPoint;
            }
            else
            {
                newOffset = new Point(de.X - dragBase.X, de.Y - dragBase.Y);
            }

            // 96845 -- only allow drops on the client area
            //
            if (!client.GetDesignerControl().ClientRectangle.Contains(convertedPoint))
            {
                copy = false;
                move = false;
                newOffset = localDragOffset;
            }

            if (newOffset != localDragOffset)
            {
                Debug.WriteLineIf(CompModSwitches.DragDrop.TraceInfo, $"\tParentControlDesigner.OnDragOver: {de}");
                DrawDragFrames(dragComps, localDragOffset, localDragEffect,
                               newOffset, de.Effect, forceDrawFrames);
                localDragOffset = newOffset;
                localDragEffect = de.Effect;
            }
        }

        if (copy)
        {
            de.Effect = DragDropEffects.Copy;
        }
        else if (move)
        {
            de.Effect = DragDropEffects.Move;
        }
        else
        {
            de.Effect = DragDropEffects.None;
        }

        if (localMoveOnly)
        {
            de.Effect |= (DragDropEffects)AllowLocalMoveOnly;
        }
    }

    public void DoOleGiveFeedback(GiveFeedbackEventArgs e)
    {
        if (selectionHandler is null)
        {
            Debug.Fail("selectionHandler should not be null");
        }

        e.UseDefaultCursors = ((!localDragInside && !forceDrawFrames) || ((e.Effect & (DragDropEffects.Copy)) != 0)) || e.Effect == DragDropEffects.None;
        if (!e.UseDefaultCursors && selectionHandler is not null)
        {
            selectionHandler.SetCursor();
        }
    }

    private static object[] GetDraggingObjects(IDataObject dataObj, bool topLevelOnly)
    {
        object[] components = null;

        if (dataObj is ComponentDataObjectWrapper)
        {
            dataObj = ((ComponentDataObjectWrapper)dataObj).InnerData;
            ComponentDataObject cdo = (ComponentDataObject)dataObj;

            components = cdo.Components;
        }

        if (!topLevelOnly || components is null)
        {
            return components;
        }

        return GetTopLevelComponents(components);
    }

    public static object[] GetDraggingObjects(IDataObject dataObj)
    {
        return GetDraggingObjects(dataObj, false);
    }

    public static object[] GetDraggingObjects(DragEventArgs de)
    {
        return GetDraggingObjects(de.Data);
    }

    private static object[] GetTopLevelComponents(ICollection comps)
    {
        // Filter the top-level components.
        //
        if (!(comps is IList))
        {
            comps = new ArrayList(comps);
        }

        IList compList = (IList)comps;
        ArrayList topLevel = new ArrayList();
        foreach (object comp in compList)
        {
            Control c = comp as Control;
            if (c is null && comp is not null)
            {
                topLevel.Add(comp);
            }
            else if (c is not null)
            {
                if (c.Parent is null || !compList.Contains(c.Parent))
                {
                    topLevel.Add(comp);
                }
            }
        }

        return topLevel.ToArray();
    }

    protected object GetService(Type t)
    {
        return serviceProvider.GetService(t);
    }

    protected virtual void OnInitializeComponent(IComponent comp, int x, int y, int width, int height, bool hasLocation, bool hasSize)
    {
    }
}
