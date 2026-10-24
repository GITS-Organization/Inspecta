import 'package:responsive_framework/responsive_framework.dart';

import '../consts/exports.dart';

class MResponsiveWrapper extends StatelessWidget {
  final Widget child;

  const MResponsiveWrapper({super.key, required this.child});

  @override
  Widget build(BuildContext context) {
    return ResponsiveBreakpoints(
      breakpoints: const [
        Breakpoint(start: 0, end: 500, name: MOBILE),
        Breakpoint(start: 431, end: 800, name: TABLET),
        Breakpoint(start: 801, end: 1000, name: DESKTOP),
        Breakpoint(start: 1001, end: 1200, name: '4K'),
      ],
      child: child,
    );
  }

  static Widget wrapper({
    required Widget child,
    required BuildContext context,
  }) {
    return MediaQuery(
      data: MediaQuery.of(context)
          .copyWith(textScaler: const TextScaler.linear(1.0)),
      child: MaxWidthBox(
        maxWidth: 1200,
        backgroundColor: Theme.of(context).colorScheme.surfaceContainer,
        child: ResponsiveScaledBox(
          width: ResponsiveValue<double>(
            context,
            defaultValue: 500,
            conditionalValues: [
              const Condition.equals(name: MOBILE, value: 500),
              const Condition.between(start: 501, end: 800, value: 800),
              const Condition.between(start: 801, end: 1000, value: 1000),
              const Condition.between(start: 1001, end: 1200, value: 1200),
            ],
          ).value,
          child: BouncingScrollWrapper.builder(
            context,
            child,
            dragWithMouse: true,
          ),
        ),
      ),
    );
  }
}
