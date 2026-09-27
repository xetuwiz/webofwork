import 'package:flutter/material.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/widget_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/services/profile_details_service.dart';
import 'package:webofwork/views/profile_details_view/components/profile_details_portfolio_card.dart';

class ProfileDetailsPortfolio extends StatelessWidget {
  final ProfileDetailsService pd;
  const ProfileDetailsPortfolio({super.key, required this.pd});

  @override
  Widget build(BuildContext context) {
    final portfolios = pd.profileDetails.portfolios;
    if (portfolios.isEmpty) return const SizedBox();
    return Container(
      margin: const EdgeInsets.only(top: 20),
      padding: const EdgeInsets.symmetric(vertical: 20),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(8),
        color: context.dProvider.whiteColor,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(LocalKeys.portfolio, style: context.titleMedium?.bold6).hp20,
          Divider(
            color: context.dProvider.black8,
            thickness: 2,
            height: 36,
          ),
          ...portfolios.asMap().entries.map(
                (entry) => Column(
                  children: [
                    ProfileDetailsPortfolioCard(item: entry.value),
                    if (entry.key != portfolios.length - 1)
                      Divider(
                        color: context.dProvider.black8,
                        thickness: 2,
                        height: 36,
                      ).hp20,
                  ],
                ),
              ),
        ],
      ),
    );
  }
}
