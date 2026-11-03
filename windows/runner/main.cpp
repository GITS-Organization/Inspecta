#include <flutter/dart_project.h>
#include <flutter/flutter_view_controller.h>
#include <windows.h>

#include "flutter_window.h"
#include "utils.h"

int APIENTRY wWinMain(_In_ HINSTANCE instance, _In_opt_ HINSTANCE prev,
                      _In_ wchar_t *command_line, _In_ int show_command) {
  // Attach to console when present (e.g., 'flutter run') or create a
  // new console when running with a debugger.
  if (!::AttachConsole(ATTACH_PARENT_PROCESS) && ::IsDebuggerPresent()) {
    CreateAndAttachConsole();
  }

  // Initialize COM, so that it is available for use in the library and/or
  // plugins.
  ::CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);

  flutter::DartProject project(L"data");

  std::vector<std::string> command_line_arguments =
      GetCommandLineArguments();

  project.set_dart_entrypoint_arguments(std::move(command_line_arguments));

  FlutterWindow window(project);
  Win32Window::Point origin(10, 10);
  Win32Window::Size size(1280, 720);
  if (!window.Create(L"Integrity", origin, size)) {
    return EXIT_FAILURE;
  }
  window.SetQuitOnClose(true);

  // Phone-sized, fixed window: no resize or maximize, centered on the work
  // area. The client area is 390x844 logical px, capped to fit the screen.
  HWND hwnd = window.GetHandle();
  LONG_PTR style = ::GetWindowLongPtr(hwnd, GWL_STYLE);
  style &= ~(WS_THICKFRAME | WS_MAXIMIZEBOX);
  ::SetWindowLongPtr(hwnd, GWL_STYLE, style);

  const UINT dpi = ::GetDpiForWindow(hwnd);
  RECT work_area;
  ::SystemParametersInfo(SPI_GETWORKAREA, 0, &work_area, 0);

  RECT frame = {0, 0, ::MulDiv(390, dpi, 96), ::MulDiv(844, dpi, 96)};
  ::AdjustWindowRectExForDpi(&frame, static_cast<DWORD>(style), FALSE, 0, dpi);
  int width = frame.right - frame.left;
  int height = frame.bottom - frame.top;
  const int max_height = work_area.bottom - work_area.top;
  if (height > max_height) {
    // Keep the phone aspect ratio when the screen is too short.
    width = ::MulDiv(width, max_height, height);
    height = max_height;
  }
  const int x = work_area.left + ((work_area.right - work_area.left) - width) / 2;
  const int y = work_area.top + (max_height - height) / 2;
  ::SetWindowPos(hwnd, nullptr, x, y, width, height,
                 SWP_NOZORDER | SWP_FRAMECHANGED);

  ::MSG msg;
  while (::GetMessage(&msg, nullptr, 0, 0)) {
    ::TranslateMessage(&msg);
    ::DispatchMessage(&msg);
  }

  ::CoUninitialize();
  return EXIT_SUCCESS;
}
