import { makeStyles } from '@material-ui/core/styles';
import { Grid, Paper, Table, TableContainer, TableBody, TableCell, TableHead, TableRow, Fab } from '@material-ui/core';
import { Gender, ExactAge, AgeInYears, RoleVersionApproval, VolunteerFamily, RemovedRole, RoleRemovalReason } from '../../GeneratedClient';
import { differenceInYears } from 'date-fns';
import { useRecoilValue } from 'recoil';
import { volunteerFamiliesData } from '../../Model/VolunteersModel';
import { policyData } from '../../Model/ConfigurationModel';
import { RoleApprovalStatus } from '../../GeneratedClient';
import React, { useState } from 'react';
import AddIcon from '@material-ui/icons/Add';
import { CreateVolunteerFamilyDialog } from './CreateVolunteerFamilyDialog';
import { useHistory } from 'react-router-dom';

const useStyles = makeStyles((theme) => ({
  paper: {
    padding: theme.spacing(2),
    display: 'flex',
    overflow: 'auto',
    flexDirection: 'column',
  },
  fixedHeight: {
    height: 240,
  },
  table: {
    minWidth: 700,
  },
  familyRow: {
    backgroundColor: '#eef'
  },
  adultRow: {
  },
  childRow: {
    color: 'ddd',
    fontStyle: 'italic'
  },
  drawerPaper: {
  },
  fabAdd: {
    position: 'fixed',
    right: '30px',
    bottom: '70px'
  }
}));

function approvalStatus(roleVersionApprovals: RoleVersionApproval[] | undefined, roleRemoval: RemovedRole | undefined) {
  return typeof roleRemoval !== 'undefined'
    ? RoleRemovalReason[roleRemoval.reason!]
    : !roleVersionApprovals
    ? "-"
    : roleVersionApprovals.some(x => x.approvalStatus === RoleApprovalStatus.Onboarded)
    ? "Onboarded"
    : roleVersionApprovals.some(x => x.approvalStatus === RoleApprovalStatus.Approved)
    ? "Approved"
    : roleVersionApprovals.some(x => x.approvalStatus === RoleApprovalStatus.Prospective)
    ? "Prospective"
    : "-";
}

function familyLastName(family: VolunteerFamily) {
  return family.family!.adults?.filter(adult => family.family!.primaryFamilyContactPersonId === adult.item1?.id)[0]?.item1?.lastName || "";
}

function VolunteerApproval() {
  const classes = useStyles();
  const history = useHistory();

  // The array object returned by Recoil is read-only. We need to copy it before we can do an in-place sort.
  const volunteerFamilies = useRecoilValue(volunteerFamiliesData).map(x => x).sort((a, b) =>
    familyLastName(a) < familyLastName(b) ? -1 : familyLastName(a) > familyLastName(b) ? 1 : 0);
  const policy = useRecoilValue(policyData);

  const volunteerFamilyRoleNames =
    (policy.volunteerPolicy?.volunteerFamilyRoles &&
    Object.entries(policy.volunteerPolicy?.volunteerFamilyRoles).map(([key]) => key))
    || [];
  const volunteerRoleNames =
    (policy.volunteerPolicy?.volunteerRoles &&
    Object.entries(policy.volunteerPolicy?.volunteerRoles).map(([key]) => key))
    || [];

  function openVolunteerFamily(volunteerFamilyId: string) {
    history.push(`/volunteers/family/${volunteerFamilyId}`);
  }
  const [createVolunteerFamilyDialogOpen, setCreateVolunteerFamilyDialogOpen] = useState(false);

  return (
    <Grid container spacing={3}>
      <Grid item xs={12}>
        <TableContainer component={Paper}>
          <Table className={classes.table} size="small">
            <TableHead>
              <TableRow>
                <TableCell>First Name</TableCell>
                <TableCell>Last Name</TableCell>
                <TableCell>Gender</TableCell>
                <TableCell>Age</TableCell>
                { volunteerFamilyRoleNames.map(roleName =>
                  (<TableCell key={roleName}>{roleName}</TableCell>))}
                { volunteerRoleNames.map(roleName =>
                  (<TableCell key={roleName}>{roleName}</TableCell>))}
              </TableRow>
            </TableHead>
            <TableBody>
              {volunteerFamilies.map((volunteerFamily) => (
                <React.Fragment key={volunteerFamily.family?.id}>
                  <TableRow className={classes.familyRow} onClick={() => openVolunteerFamily(volunteerFamily.family!.id!)}>
                    <TableCell key="1" colSpan={4}>{familyLastName(volunteerFamily) + " Family"
                    }</TableCell>
                    { volunteerFamilyRoleNames.map(roleName =>
                      (<TableCell key={roleName}>{
                        approvalStatus(volunteerFamily.familyRoleApprovals?.[roleName], volunteerFamily.removedRoles?.find(x => x.roleName === roleName))
                      }</TableCell>))}
                    <TableCell colSpan={volunteerRoleNames.length} />
                  </TableRow>
                  {volunteerFamily.family?.adults?.map(adult => adult.item1 && (
                    <TableRow key={volunteerFamily.family?.id + ":" + adult.item1.id}
                      onClick={() => openVolunteerFamily(volunteerFamily.family!.id!)}
                      className={classes.adultRow}>
                      <TableCell>{adult.item1.firstName}</TableCell>
                      <TableCell>{adult.item1.lastName}</TableCell>
                      <TableCell>{typeof(adult.item1.gender) === 'undefined' ? "" : Gender[adult.item1.gender]}</TableCell>
                      <TableCell align="right">
                        { adult.item1.age instanceof ExactAge
                          ? adult.item1.age.dateOfBirth && differenceInYears(new Date(), adult.item1.age.dateOfBirth)
                          : adult.item1.age instanceof AgeInYears
                          ? adult.item1.age.years && adult.item1?.age.asOf && (adult.item1.age.years + differenceInYears(new Date(), adult.item1.age.asOf))
                          : "⚠" }
                      </TableCell>
                      <TableCell colSpan={volunteerFamilyRoleNames.length} />
                      { volunteerRoleNames.map(roleName =>
                        (<TableCell key={roleName}>{
                          approvalStatus(volunteerFamily.individualVolunteers?.[adult.item1?.id || '']?.individualRoleApprovals?.[roleName],
                            volunteerFamily.individualVolunteers?.[adult.item1?.id || '']?.removedRoles?.find(x => x.roleName === roleName))
                        }</TableCell>))}
                    </TableRow>
                  ))}
                  {volunteerFamily.family?.children?.map(child => (
                    <TableRow key={volunteerFamily.family?.id + ":" + child.id}
                      onClick={() => openVolunteerFamily(volunteerFamily.family!.id!)}
                      className={classes.childRow}>
                      <TableCell>{child.firstName}</TableCell>
                      <TableCell>{child.lastName}</TableCell>
                      <TableCell>{typeof(child.gender) === 'undefined' ? "" : Gender[child.gender]}</TableCell>
                      <TableCell align="right">
                        { child.age instanceof ExactAge
                          ? child.age.dateOfBirth && differenceInYears(new Date(), child.age.dateOfBirth)
                          : child.age instanceof AgeInYears
                          ? child.age.years && child.age.asOf && (child.age.years + differenceInYears(new Date(), child.age.asOf))
                          : "⚠" }
                      </TableCell>
                      <TableCell colSpan={
                        volunteerFamilyRoleNames.length +
                        volunteerRoleNames.length } />
                    </TableRow>
                  ))}
                </React.Fragment>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
        <Fab color="primary" aria-label="add" className={classes.fabAdd}
          onClick={() => setCreateVolunteerFamilyDialogOpen(true)}>
          <AddIcon />
        </Fab>
        {createVolunteerFamilyDialogOpen && <CreateVolunteerFamilyDialog onClose={(volunteerFamilyId) => {
          setCreateVolunteerFamilyDialogOpen(false);
          volunteerFamilyId && history.push(`/volunteers/family/${volunteerFamilyId}`);
        }} />}
      </Grid>
    </Grid>
  );
}

export { VolunteerApproval };
