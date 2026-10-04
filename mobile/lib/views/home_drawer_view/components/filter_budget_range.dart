import 'package:flutter/material.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';

import '../../../view_models/home_drawer_view_model/home_drawer_view_model.dart';

/// Min / max budget. Sends `min_price` and `max_price`, which
/// `ProjectsController.FreelancerList` does bind.
///
/// This used to wrap the two fields in a `ValueListenableBuilder` over a
/// `RangeValues` that nothing ever wrote to, so the rebuild it existed to trigger
/// could not fire. The fields are free text, not a slider, so there is no range to
/// observe.
class FilterBudgetRange extends StatelessWidget {
  const FilterBudgetRange({
    super.key,
  });

  @override
  Widget build(BuildContext context) {
    final hdm = HomeDrawerViewModel.instance;
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(8),
        color: context.dProvider.whiteColor,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            LocalKeys.jobPriceBudget.capitalizeWords,
            style: context.titleMedium?.bold6,
          ),
          16.toHeight,
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Expanded(
                flex: 1,
                child: TextFormField(
                  controller: hdm.minPriceController,
                  keyboardType: TextInputType.number,
                  decoration: InputDecoration(
                    hintText: LocalKeys.min,
                  ),
                ),
              ),
              Container(
                width: 16,
                padding: const EdgeInsets.symmetric(horizontal: 4),
                child: Divider(
                  thickness: 2,
                  color: context.dProvider.black7,
                ),
              ),
              Expanded(
                flex: 1,
                child: TextFormField(
                  controller: hdm.maxPriceController,
                  keyboardType: TextInputType.number,
                  decoration: InputDecoration(
                    hintText: LocalKeys.max,
                  ),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
