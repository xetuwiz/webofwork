import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:webofwork/services/recommendations_service.dart';
import 'package:webofwork/views/home_view/components/project_card.dart';

class ProjectRecommendationsWidget extends StatefulWidget {
  const ProjectRecommendationsWidget({super.key});

  @override
  State<ProjectRecommendationsWidget> createState() => _ProjectRecommendationsWidgetState();
}

class _ProjectRecommendationsWidgetState extends State<ProjectRecommendationsWidget> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<RecommendationsService>().fetchRecommendations();
    });
  }

  @override
  Widget build(BuildContext context) {
    return Consumer<RecommendationsService>(
      builder: (context, rs, child) {
        if (rs.isLoading && rs.recommendedProjects.isEmpty) {
          return const Center(child: CircularProgressIndicator());
        }

        if (rs.recommendedProjects.isEmpty) {
          return Padding(
            padding: const EdgeInsets.symmetric(vertical: 24),
            child: Center(
              child: Text(
                'No recommendations right now',
                style: context.titleSmall?.copyWith(color: context.dProvider.black5),
              ),
            ),
          );
        }

        return Column(
          children: rs.recommendedProjects.map((job) {
            return Padding(
              padding: const EdgeInsets.only(bottom: 12.0),
              child: ProjectCard(
                id: job.id,
                title: job.title,
                createDate: job.createdAt,
                expertise: job.categoryName.toString().capitalize,
                price: job.budget,
                priceType: job.pricingType.toString().capitalize,
                summery: job.description,
                tags: job.projectSkills.map((s) => s.skill).toList(),
                // `ProjectResponse` carries no location, no proposal count and
                // no verification flag. This card used to pass a literal
                // 'Remote' and `userVerified: false`, so the dashboard
                // advertised a location and an unverified status for real
                // clients on every recommendation. Null omits the rows.
                location: null,
                proposalCount: null,
                userVerified: null,
                deadline: job.duration,
                isFavorite: false,
                fromDetails: false,
              ),
            );
          }).toList(),
        );
      },
    );
  }
}
