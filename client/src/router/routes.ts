import { RouteRecordRaw } from 'vue-router';
import { mustBeAdmin, mustBeAuth, mustNotBeAuth } from './routesGuards';

const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'login',
    component: () => import('pages/LoginPage.vue'),
    beforeEnter: mustNotBeAuth,
  },
  {
    path: '/',
    beforeEnter: mustBeAuth,
    children: [
      {
        path: '',
        name: 'home',
        component: () => import('src/pages/projects/ProjectbuildPage.vue'),
      },
      {
        path: 'admin-dashboard',
        beforeEnter: mustBeAdmin,
        children: [
          {
            path: '',
            name: 'admin-dashboard',
            component: () => import('pages/administration/DashboardAdminPage.vue')
          },
          {
            path: 'manage-projects',
            name: 'admin-projects',
            component: () => import('pages/administration/ManageProjectsPage.vue')
          },
          {
            path: 'manage-users',
            name: 'admin-users',
            component: () => import('pages/administration/ManageUsersPage.vue'),
          },
          {
            path: 'admin-pulling-overview',
            name: 'admin-pulling',
            component: () => import('pages/administration/PullingOverviewPage.vue'),
          },
        ]
      },
      {
        // This route accept an optional query parameter: ?jobId=<jobId>
        path: 'projets/:id',
        name: 'project-show-root',
        beforeEnter: mustBeAuth,
        children: [
          {
            path: '',
            name: 'project-show',
            component: () => import('pages/projects/ProjectPage.vue'),
          },
          {
            path: 'processes/:processId/pipeline-fork',
            name: 'pipeline-fork',
            component: () => import('pages/processes/ForkPipelineEditor.vue'),
            beforeEnter: mustBeAuth,
          },
          {
            path: 'processes/:processId/models/:modelId',
            beforeEnter: mustBeAuth,
            children: [
              {
                path: '',
                name: 'model-show',
                component: () => import('pages/models/ModelPage.vue'),
              },
              {
                path: 'test',
                name: 'model-test',
                component: () => import('pages/models/ModelTestPage.vue'),
                beforeEnter: mustBeAuth,
              },
            ],
          },
        ],
      },
    ],
  },

  // // Always leave this as last one,
  // // but you can also remove it
  // {
  //   path: '/:catchAll(.*)*',
  //   name: 'error',
  //   component: () => import('pages/ErrorNotFound.vue'),
  // },
];

export default routes;
