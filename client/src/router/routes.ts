import { RouteRecordRaw } from "vue-router";
import { mustBeAdmin, mustBeAuth, mustNotBeAuth } from "./routesGuards";

const routes: RouteRecordRaw[] = [
  {
    path: "/login",
    name: "login",
    component: () => import("pages/LoginPage.vue"),
    beforeEnter: mustNotBeAuth,
  },
  {
    path: "/",
    beforeEnter: mustBeAuth,
    children: [
      {
        path: "",
        name: "home",
        component: () => import("src/pages/projects/ProjectbuildPage.vue"),
      },
      {
        path: "manage-users",
        name: "admin-users",
        component: () => import("pages/administration/ManageUsersPage.vue"),
        beforeEnter: mustBeAdmin,
      },
      {
        // This route accept an optional query parameter: ?jobId=<jobId>
        path: "projets/:id",
        name: "project-show-root",
        beforeEnter: mustBeAuth,
        children: [
          {
            path: "",
            name: "project-show",
            component: () => import("pages/projects/ProjectPage.vue"),
          },
        ],
      },
      {
        path: "models/:id",
        name: "model-show",
        component: () => import("pages/models/ModelPage.vue"),
        beforeEnter: mustBeAuth,
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
