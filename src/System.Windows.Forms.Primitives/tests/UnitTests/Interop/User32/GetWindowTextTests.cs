﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Windows.Forms.Primitives.Tests.Interop.User32;

public class GetWindowTextTests
{
    [StaFact]
    public void GetWindowText_DoesNotTruncateText()
    {
        CallGetWindowText(useBeforeGetTextLengthCallback: false);
    }

    [StaFact]
    public void GetWindowText_DoesNotLoopInfinitely()
    {
        CallGetWindowText(useBeforeGetTextLengthCallback: true);
    }

    private void CallGetWindowText(bool useBeforeGetTextLengthCallback)
    {
        const string shortText = "A";

        // Use a long string that exceeds the initial buffer size (16).
        string longText = new string('X', 50);

        var windowClass = new ChangeWindowTextClass();
        windowClass.Register();
        HWND windowHandle = (HWND)windowClass.CreateWindow(shortText);

        windowClass.BeforeGetTextCallback = () => longText;
        if (useBeforeGetTextLengthCallback)
        {
            windowClass.BeforeGetTextLengthCallback = () => shortText;
        }

        string result = PInvoke.GetWindowText(windowHandle);
        PInvoke.DestroyWindow(windowHandle);

        Assert.Equal(longText, result);
    }

    private class ChangeWindowTextClass : WindowClass
    {
        public Func<string>? BeforeGetTextLengthCallback
        {
            get;
            set;
        }

        public Func<string>? BeforeGetTextCallback
        {
            get;
            set;
        }

        protected override LRESULT WNDPROC(HWND hWnd, MessageId msg, WPARAM wParam, LPARAM lParam)
        {
            switch (msg)
            {
                case PInvoke.WM_GETTEXTLENGTH:
                    string? text = BeforeGetTextLengthCallback?.Invoke();
                    if (text is not null)
                    {
                        PInvoke.SetWindowText(hWnd, text);
                    }

                    break;
                case PInvoke.WM_GETTEXT:
                    text = BeforeGetTextCallback?.Invoke();
                    if (text is not null)
                    {
                        PInvoke.SetWindowText(hWnd, text);
                    }

                    break;
            }

            return base.WNDPROC(hWnd, msg, wParam, lParam);
        }
    }
}
