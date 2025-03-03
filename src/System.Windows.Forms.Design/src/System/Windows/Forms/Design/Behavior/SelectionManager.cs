﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing;

namespace System.Windows.Forms.Design.Behavior;

/// <summary>
///  The SelectionBehavior is pushed onto the BehaviorStack in response to a positively hit tested SelectionGlyph.  The SelectionBehavior performs  two main tasks: 1) forward messages to the related ControlDesigner, and 2) calls upon the SelectionManager to push a potential DragBehavior.
/// </summary>
internal sealed class SelectionManager : IDisposable
{
    private Adorner _selectionAdorner;                  //used to provide all selection glyphs
    private Adorner _bodyAdorner;                       //used to track all body glyphs for each control
    private BehaviorService _behaviorService;           //ptr back to our BehaviorService
    private IServiceProvider _serviceProvider;          //standard service provider
    private readonly Dictionary<IComponent, ControlDesigner> _componentToDesigner;    //used for quick look up of designers related to components
    private readonly Control _rootComponent;            //root component being designed
    private ISelectionService _selSvc;                  //we cache the selection service for perf.
    private IDesignerHost _designerHost;                //we cache the designerhost for perf.
    private bool _needRefresh;                          // do we need to refresh?
    private Rectangle[] _prevSelectionBounds;           //used to only repaint the changing part of the selection
    private object _prevPrimarySelection;               //used to check if the primary selection changed
    private Rectangle[] _curSelectionBounds;
    private int _curCompIndex;
    private DesignerActionUI _designerActionUI;         // the "container" for all things related to the designer action (smarttags) UI
    private bool _selectionChanging;                    //we don't want the OnSelectionChanged to be recursively called.

    /// <summary>
    ///  Constructor.  Here we query for necessary services and cache them for perf. reasons. We also hook to Component Added/Removed/Changed notifications so we can keep in sync when the designers' components change.  Also, we create our custom Adorner and add it to the BehaviorService.
    /// </summary>
    public SelectionManager(IServiceProvider serviceProvider, BehaviorService behaviorService)
    {
        _prevSelectionBounds = null;
        _prevPrimarySelection = null;
        _behaviorService = behaviorService;
        _serviceProvider = serviceProvider;

        _selSvc = (ISelectionService)serviceProvider.GetService(typeof(ISelectionService));
        _designerHost = (IDesignerHost)serviceProvider.GetService(typeof(IDesignerHost));

        if (_designerHost is null || _selSvc is null)
        {
            Debug.Fail("SelectionManager - Host or SelSvc is null, can't continue");
        }

        //sync the BehaviorService's begindrag event
        behaviorService.BeginDrag += new BehaviorDragDropEventHandler(OnBeginDrag);

        //sync the BehaviorService's Synchronize event
        behaviorService.Synchronize += new EventHandler(OnSynchronize);

        _selSvc.SelectionChanged += new EventHandler(OnSelectionChanged);
        _rootComponent = (Control)_designerHost.RootComponent;

        //create and add both of our adorners,
        //one for selection, one for bodies
        _selectionAdorner = new Adorner();
        _bodyAdorner = new Adorner();
        behaviorService.Adorners.Add(_bodyAdorner);
        behaviorService.Adorners.Add(_selectionAdorner); // adding this will cause the adorner to get setup with a ptr
                                                         // to the beh.svc.

        _componentToDesigner = new();

        IComponentChangeService cs = (IComponentChangeService)serviceProvider.GetService(typeof(IComponentChangeService));
        if (cs is not null)
        {
            cs.ComponentAdded += new ComponentEventHandler(OnComponentAdded);
            cs.ComponentRemoved += new ComponentEventHandler(OnComponentRemoved);
            cs.ComponentChanged += new ComponentChangedEventHandler(OnComponentChanged);
        }

        _designerHost.TransactionClosed += new DesignerTransactionCloseEventHandler(OnTransactionClosed);

        // designeraction UI
        if (_designerHost.GetService(typeof(DesignerOptionService)) is DesignerOptionService options)
        {
            PropertyDescriptor p = options.Options.Properties["UseSmartTags"];
            if (p is not null && p.PropertyType == typeof(bool) && (bool)p.GetValue(null))
            {
                _designerActionUI = new DesignerActionUI(serviceProvider, _selectionAdorner);
                behaviorService.DesignerActionUI = _designerActionUI;
            }
        }
    }

    /// <summary>
    ///  Returns the Adorner that contains all the BodyGlyphs for the current selection state.
    /// </summary>
    internal Adorner BodyGlyphAdorner
    {
        get => _bodyAdorner;
    }

    /// <summary>
    ///  There are certain cases like Adding Item to ToolStrips through InSitu Editor, where there is
    ///  ParentTransaction that has to be cancelled depending upon the user action When this parent transaction is
    ///  cancelled, there may be no reason to REFRESH the selectionManager which actually clears all the glyphs and
    ///  readds them This REFRESH causes a lot of flicker and can be avoided by setting this property to false.
    ///  Since this property is checked in the TransactionClosed, the SelectionManager won't REFRESH and hence
    ///  just eat up the refresh thus avoiding unnecessary flicker.
    /// </summary>
    internal bool NeedRefresh
    {
        get => _needRefresh;
        set => _needRefresh = value;
    }

    /// <summary>
    ///  Returns the Adorner that contains all the BodyGlyphs for the current selection state.
    /// </summary>
    internal Adorner SelectionGlyphAdorner
    {
        get => _selectionAdorner;
    }

    /// <summary>
    ///  This method fist calls the recursive AddControlGlyphs() method. When finished, we add the final glyph(s)
    ///  to the root comp.
    /// </summary>
    private void AddAllControlGlyphs(Control parent, List<IComponent> selComps, object primarySelection)
    {
        foreach (Control control in parent.Controls)
        {
            AddAllControlGlyphs(control, selComps, primarySelection);
        }

        GlyphSelectionType selType = GlyphSelectionType.NotSelected;
        if (selComps.Contains(parent))
        {
            if (parent.Equals(primarySelection))
            {
                selType = GlyphSelectionType.SelectedPrimary;
            }
            else
            {
                selType = GlyphSelectionType.Selected;
            }
        }

        AddControlGlyphs(parent, selType);
    }

    /// <summary>
    ///  Recursive method that goes through and adds all the glyphs of every child to our global Adorner.
    /// </summary>
    private void AddControlGlyphs(Control control, GlyphSelectionType selType)
    {
        if (_componentToDesigner.TryGetValue(control, out ControlDesigner controlDesigner) && controlDesigner is not null)
        {
            ControlBodyGlyph bodyGlyph = controlDesigner.GetControlGlyphInternal(selType);
            if (bodyGlyph is not null)
            {
                _bodyAdorner.Glyphs.Add(bodyGlyph);
                if (selType == GlyphSelectionType.SelectedPrimary ||
                    selType == GlyphSelectionType.Selected)
                {
                    if (_curSelectionBounds[_curCompIndex] == Rectangle.Empty)
                    {
                        _curSelectionBounds[_curCompIndex] = bodyGlyph.Bounds;
                    }
                    else
                    {
                        _curSelectionBounds[_curCompIndex] = Rectangle.Union(_curSelectionBounds[_curCompIndex], bodyGlyph.Bounds);
                    }
                }
            }

            GlyphCollection glyphs = controlDesigner.GetGlyphs(selType);
            if (glyphs is not null)
            {
                _selectionAdorner.Glyphs.AddRange(glyphs);
                if (selType == GlyphSelectionType.SelectedPrimary ||
                    selType == GlyphSelectionType.Selected)
                {
                    foreach (Glyph glyph in glyphs)
                    {
                        _curSelectionBounds[_curCompIndex] = Rectangle.Union(_curSelectionBounds[_curCompIndex], glyph.Bounds);
                    }
                }
            }
        }

        if (selType == GlyphSelectionType.SelectedPrimary || selType == GlyphSelectionType.Selected)
        {
            _curCompIndex++;
        }
    }

    /// <summary>
    ///  Unhook all of our event notifications, clear our adorner and remove it from the Beh.Svc.
    /// </summary>
    // We don't need to Dispose rootComponent.
    public void Dispose()
    {
        if (_designerHost is not null)
        {
            _designerHost.TransactionClosed -= new DesignerTransactionCloseEventHandler(OnTransactionClosed);
            _designerHost = null;
        }

        if (_serviceProvider is not null)
        {
            IComponentChangeService cs = (IComponentChangeService)_serviceProvider.GetService(typeof(IComponentChangeService));
            if (cs is not null)
            {
                cs.ComponentAdded -= new ComponentEventHandler(OnComponentAdded);
                cs.ComponentChanged -= new ComponentChangedEventHandler(OnComponentChanged);
                cs.ComponentRemoved -= new ComponentEventHandler(OnComponentRemoved);
            }

            if (_selSvc is not null)
            {
                _selSvc.SelectionChanged -= new EventHandler(OnSelectionChanged);
                _selSvc = null;
            }

            _serviceProvider = null;
        }

        if (_behaviorService is not null)
        {
            _behaviorService.Adorners.Remove(_bodyAdorner);
            _behaviorService.Adorners.Remove(_selectionAdorner);
            _behaviorService.BeginDrag -= new BehaviorDragDropEventHandler(OnBeginDrag);
            _behaviorService.Synchronize -= new EventHandler(OnSynchronize);
            _behaviorService = null;
        }

        if (_selectionAdorner is not null)
        {
            _selectionAdorner.Glyphs.Clear();
            _selectionAdorner = null;
        }

        if (_bodyAdorner is not null)
        {
            _bodyAdorner.Glyphs.Clear();
            _bodyAdorner = null;
        }

        if (_designerActionUI is not null)
        {
            _designerActionUI.Dispose();
            _designerActionUI = null;
        }
    }

    /// <summary>
    ///  Refreshes all selection Glyphs.
    /// </summary>
    public void Refresh()
    {
        NeedRefresh = false;
        OnSelectionChanged(this, null);
    }

    /// <summary>
    ///  When a component is added, we get the designer and add it to our hashtable for quick lookup.
    /// </summary>
    private void OnComponentAdded(object source, ComponentEventArgs ce)
    {
        IComponent component = ce.Component;
        IDesigner designer = _designerHost.GetDesigner(component);
        if (designer is ControlDesigner controlDesigner)
        {
            _componentToDesigner.Add(component, controlDesigner);
        }
    }

    /// <summary>
    ///  Before a drag, remove all glyphs that are involved in the drag operation and any that don't allow drops.
    /// </summary>
    private void OnBeginDrag(object source, BehaviorDragDropEventArgs e)
    {
        List<IComponent> dragComps = e.DragComponents.Cast<IComponent>().ToList();
        List<Glyph> glyphsToRemove = new();
        foreach (ControlBodyGlyph g in _bodyAdorner.Glyphs)
        {
            if (g.RelatedComponent is Control control && (dragComps.Contains(g.RelatedComponent) || !control.AllowDrop))
            {
                glyphsToRemove.Add(g);
            }
        }

        foreach (Glyph g in glyphsToRemove)
        {
            _bodyAdorner.Glyphs.Remove(g);
        }
    }

    // Called by the DropSourceBehavior when dragging into a new host
    internal void OnBeginDrag(BehaviorDragDropEventArgs e)
    {
        OnBeginDrag(null, e);
    }

    /// <summary>
    ///  When a component is changed - we need to refresh the selection.
    /// </summary>
    private void OnComponentChanged(object source, ComponentChangedEventArgs ce)
    {
        if (_selSvc.GetComponentSelected(ce.Component))
        {
            if (!_designerHost.InTransaction)
            {
                Refresh();
            }
            else
            {
                NeedRefresh = true;
            }
        }
    }

    /// <summary>
    ///  When a component is removed - we remove the key and value from our hashtable.
    /// </summary>
    private void OnComponentRemoved(object source, ComponentEventArgs ce)
    {
            _componentToDesigner.Remove(ce.Component);

        //remove the associated designeractionpanel
        _designerActionUI?.RemoveActionGlyph(ce.Component);
    }

    /// <summary>
    ///  Computes the region representing the difference between the old selection and the new selection.
    /// </summary>
    private Region DetermineRegionToRefresh(object primarySelection)
    {
        Region toRefresh = new Region(Rectangle.Empty);
        Rectangle[] larger;
        Rectangle[] smaller;
        if (_curSelectionBounds.Length >= _prevSelectionBounds.Length)
        {
            larger = _curSelectionBounds;
            smaller = _prevSelectionBounds;
        }
        else
        {
            larger = _prevSelectionBounds;
            smaller = _curSelectionBounds;
        }

        // we need to make sure all of the rects in the smaller array are
        // accounted for.  Any that don't intersect a rect in the larger
        // array need to be included in the region to repaint.
        bool[] intersected = new bool[smaller.Length];
        for (int i = 0; i < smaller.Length; i++)
        {
            intersected[i] = false;
        }

        // determine which rects in the larger array need to be
        // included in the region to invalidate by intersecting
        // with rects in the smaller array.
        for (int l = 0; l < larger.Length; l++)
        {
            bool largeIntersected = false;
            Rectangle large = larger[l];
            for (int s = 0; s < smaller.Length; s++)
            {
                if (large.IntersectsWith(smaller[s]))
                {
                    Rectangle small = smaller[s];
                    largeIntersected = true;
                    if (large != small)
                    {
                        toRefresh.Union(large);
                        toRefresh.Union(small);
                    }

                    intersected[s] = true;
                    break;
                }
            }

            if (!largeIntersected)
            {
                toRefresh.Union(large);
            }
        }

        // now add any rects from the smaller array that weren't accounted for
        for (int k = 0; k < intersected.Length; k++)
        {
            if (!intersected[k])
            {
                toRefresh.Union(smaller[k]);
            }
        }

        using Graphics g = _behaviorService.AdornerWindowGraphics;

        // If all that changed was the primary selection, then the refresh region was empty, but we do need to update the 2 controls.
        if (toRefresh.IsEmpty(g) && primarySelection is not null && !primarySelection.Equals(_prevPrimarySelection))
        {
            for (int i = 0; i < _curSelectionBounds.Length; i++)
            {
                toRefresh.Union(_curSelectionBounds[i]);
            }
        }

        return toRefresh;
    }

    /// <summary>
    ///  Event handler for the behaviorService's Synchronize event
    /// </summary>
    private void OnSynchronize(object sender, EventArgs e)
    {
        Refresh();
    }

    /// <summary>
    ///  On every selectionchange, we remove all glyphs, get the newly selected components, and re-add all glyphs back to the Adorner.
    /// </summary>
    private void OnSelectionChanged(object sender, EventArgs e)
    {
        // Note: selectionChanging would guard against a re-entrant code...
        // Since we don't want to be in messed up state when adding new Glyphs.
        if (!_selectionChanging)
        {
            _selectionChanging = true;

            _selectionAdorner.Glyphs.Clear();
            _bodyAdorner.Glyphs.Clear();

            List<IComponent> selComps = _selSvc.GetSelectedComponents().Cast<IComponent>().ToList();
            object primarySelection = _selSvc.PrimarySelection;

            //add all control glyphs to all controls on rootComp
            _curCompIndex = 0;
            _curSelectionBounds = new Rectangle[selComps.Count];
            AddAllControlGlyphs(_rootComponent, selComps, primarySelection);

            if (_prevSelectionBounds is not null)
            {
                Region toUpdate = DetermineRegionToRefresh(primarySelection);
                using Graphics g = _behaviorService.AdornerWindowGraphics;
                if (!toUpdate.IsEmpty(g))
                {
                    _selectionAdorner.Invalidate(toUpdate);
                }
            }
            else
            {
                // There was no previous selection, so just invalidate
                // the current selection
                if (_curSelectionBounds.Length > 0)
                {
                    Rectangle toUpdate = _curSelectionBounds[0];
                    for (int i = 1; i < _curSelectionBounds.Length; i++)
                    {
                        toUpdate = Rectangle.Union(toUpdate, _curSelectionBounds[i]);
                    }

                    if (toUpdate != Rectangle.Empty)
                    {
                        _selectionAdorner.Invalidate(toUpdate);
                    }
                }
                else
                {
                    _selectionAdorner.Invalidate();
                }
            }

            _prevPrimarySelection = primarySelection;
            if (_curSelectionBounds.Length > 0)
            {
                _prevSelectionBounds = new Rectangle[_curSelectionBounds.Length];
                Array.Copy(_curSelectionBounds, _prevSelectionBounds, _curSelectionBounds.Length);
            }
            else
            {
                _prevSelectionBounds = null;
            }

            _selectionChanging = false;
        }
    }

    /// <summary>
    ///  When a transaction that involves one of our components closes,  refresh to reflect any changes.
    /// </summary>
    private void OnTransactionClosed(object sender, DesignerTransactionCloseEventArgs e)
    {
        if (e.LastTransaction && NeedRefresh)
        {
            Refresh();
        }
    }
}
