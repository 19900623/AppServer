
import { SET_USERS, SET_ADMINS, SET_NEW_ADMINS, SET_OWNER, SET_OPTIONS, SET_FILTER, SET_LOGO_TEXT, SET_LOGO_SIZES, SET_LOGO_URLS, SELECT_USER, DESELECT_USER, SET_SELECTED, ADD_ADMINS, REMOVE_ADMINS } from "./actions";
import { api } from "asc-web-common";
import { isUserSelected, skipUser, getUsersBySelected, combineAdmins, removeAdmins } from "./selectors";
const { Filter } = api;

const initialState = {
  common: {
    whiteLabel: {
      logoSizes: [],
      logoText: null,
      logoUrls: []
    }
  },
  security: {
    accessRight: {
      options: [],
      users: [],
      admins: [],
      newAdmins: [],
      owner: {},
      filter: Filter.getDefault(),
      selection: [],
      selected: "none"
    }
  },
};

const peopleReducer = (state = initialState, action) => {
  const currentAdmins = state.security.accessRight.newAdmins && state.security.accessRight.newAdmins.length > 0
    ? state.security.accessRight.newAdmins
    : state.security.accessRight.admins

  switch (action.type) {
    case SET_OPTIONS:
      return Object.assign({}, state, {
        security: Object.assign({}, state.security, {
          accessRight: Object.assign({}, state.security.accessRight, {
            options: action.options
          })
        })
      });
    case SET_USERS:
      return Object.assign({}, state, {
        security: Object.assign({}, state.security, {
          accessRight: Object.assign({}, state.security.accessRight, {
            users: action.users
          })
        })
      });
    case SET_ADMINS:
      return Object.assign({}, state, {
        security: Object.assign({}, state.security, {
          accessRight: Object.assign({}, state.security.accessRight, {
            admins: action.admins
          })
        })
      });
    case ADD_ADMINS:
      return Object.assign({}, state, {
        security: Object.assign({}, state.security, {
          accessRight: Object.assign({}, state.security.accessRight, {
            newAdmins: combineAdmins(currentAdmins, action.admins)
          })
        })
      });

    case REMOVE_ADMINS:
      return Object.assign({}, state, {
        security: Object.assign({}, state.security, {
          accessRight: Object.assign({}, state.security.accessRight, {
            newAdmins: removeAdmins(currentAdmins, action.adminsId)
          })
        })
      });

    case SET_NEW_ADMINS:
      return Object.assign({}, state, {
        security: Object.assign({}, state.security, {
          accessRight: Object.assign({}, state.security.accessRight, {
            newAdmins: action.newAdmins
          })
        })
      });
    case SET_OWNER:
      return Object.assign({}, state, {
        security: Object.assign({}, state.security, {
          accessRight: Object.assign({}, state.security.accessRight, {
            owner: action.owner
          })
        })
      });
    case SET_FILTER:
      return Object.assign({}, state, {
        security: Object.assign({}, state.security, {
          accessRight: Object.assign({}, state.security.accessRight, {
            filter: action.filter
          })
        })
      });

    case SET_LOGO_TEXT:
      return Object.assign({}, state, {
        common: {
          ...state.common, whiteLabel: { ...state.common.whiteLabel, logoText: action.text }
        }
      });

    case SET_LOGO_SIZES:
      return Object.assign({}, state, {
        common: {
          ...state.common, whiteLabel: { ...state.common.whiteLabel, logoSizes: action.sizes, }
        }
      });

    case SET_LOGO_URLS:
      return Object.assign({}, state, {
        common: {
          ...state.common, whiteLabel: { ...state.common.whiteLabel, logoUrls: action.urls }
        }
      });
    case SET_SELECTED:
      return Object.assign({}, state, {
        security: Object.assign({}, state.security, {
          accessRight: Object.assign({}, state.security.accessRight, {
            selected: action.selected,
            selection: getUsersBySelected(currentAdmins, action.selected)
          })
        })
      });

    case SELECT_USER:
      if (!isUserSelected(state.security.accessRight.selection, action.user.id)) {
        return Object.assign({}, state, {
          security: Object.assign({}, state.security, {
            accessRight: Object.assign({}, state.security.accessRight, {
              selection: [...state.security.accessRight.selection, action.user]
            })
          })
        });
      } else return state;

    case DESELECT_USER:
      if (isUserSelected(state.security.accessRight.selection, action.user.id)) {
        return Object.assign({}, state, {
          security: Object.assign({}, state.security, {
            accessRight: Object.assign({}, state.security.accessRight, {
              selection: skipUser(state.security.accessRight.selection, action.user.id)
            })
          })
        });
      } else return state;

    default:
      return state;
  }
};


export default peopleReducer;