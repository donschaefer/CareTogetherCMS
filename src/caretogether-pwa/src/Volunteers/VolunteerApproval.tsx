import { Grid, Table, TableContainer, TableBody, TableCell, TableHead, TableRow, Fab, useMediaQuery, useTheme, Button, ButtonGroup, MenuItem, Select, ListItemText, Checkbox, FormControl, InputBase, SelectChangeEvent, IconButton, Snackbar, Stack, ToggleButton, ToggleButtonGroup, Typography, Box } from '@mui/material';
import { CombinedFamilyInfo, EmailAddress, Permission, VolunteerInfo } from '../GeneratedClient';
import { atom, selector, useRecoilState, useRecoilValue } from 'recoil';
import { volunteerFamiliesData } from '../Model/VolunteersModel';
import { organizationConfigurationQuery, policyData } from '../Model/ConfigurationModel';
import { RoleApprovalStatus } from '../GeneratedClient';
import React, { useEffect, useState } from 'react';
import AddIcon from '@mui/icons-material/Add';
import SmsIcon from '@mui/icons-material/Sms';
import EmailIcon from '@mui/icons-material/Email';
import FilterListIcon from '@mui/icons-material/FilterList';
import { CreateVolunteerFamilyDialog } from './CreateVolunteerFamilyDialog';
import { Link, useLocation } from 'react-router-dom';
import { SearchBar } from '../Shell/SearchBar';
import { useLocalStorage } from '../Hooks/useLocalStorage';
import { useScrollMemory } from '../Hooks/useScrollMemory';
import { useAllVolunteerFamiliesPermissions } from '../Model/SessionModel';
import { BulkSmsSideSheet } from './BulkSmsSideSheet';
import { useWindowSize } from '../Hooks/useWindowSize';
import useScreenTitle from '../Shell/ShellScreenTitle';
import UnfoldLessIcon from '@mui/icons-material/UnfoldLess';
import UnfoldMoreIcon from '@mui/icons-material/UnfoldMore';
import { useLoadable } from '../Hooks/useLoadable';
import { ProgressBackdrop } from '../Shell/ProgressBackdrop';
import { selectedLocationContextState } from '../Model/Data';
import { useAppNavigate } from '../Hooks/useAppNavigate';
import { VolunteerRoleApprovalStatusChip } from './VolunteerRoleApprovalStatusChip';

//#region Role/Status Selection code
enum filterType {
	Family = 1,
	Individual = 2
}
type filterOption = {
	key: string;
	value: string | undefined;
	type?: filterType | undefined;
	selected: boolean;
}
const roleFiltersState = atom({
	key: 'newRoleFiltersState',
	default: selector({
		key: 'newRoleFiltersState/Default',
		get: ({ get }) => {
			const policy = get(policyData);
			const familyRoles = [...Object.keys(policy.volunteerPolicy?.volunteerFamilyRoles || {})];
			const individualRoles = [...Object.keys(policy.volunteerPolicy?.volunteerRoles || {})];
			const combinedRoles = ["Not Applied",...familyRoles, ...individualRoles];
			const roleFilters: filterOption[] = [];
			for (let i = 0; i < combinedRoles.length; i++) {
				const isIndividualRole = i >= (familyRoles.length + 1);
				const roleType = isIndividualRole ? filterType.Individual : 
					combinedRoles[i] === "Not Applied" ? undefined : filterType.Family;				
				roleFilters.push({
					key: combinedRoles[i],
					value: i.toString(),
					selected: false,
					type: roleType
				});
			}
			return roleFilters;
		}
	})
});
const statusFiltersState = atom({
	key: 'statusFiltersState',
	default: selector({
		key: 'statusFiltersState/Default',
		get: () => {
			const options = [
				{ key: "Not Applied", value: 0 },
				{ key: RoleApprovalStatus[RoleApprovalStatus.Prospective], value: RoleApprovalStatus.Prospective },
				{ key: RoleApprovalStatus[RoleApprovalStatus.Approved], value: RoleApprovalStatus.Approved },
				{ key: RoleApprovalStatus[RoleApprovalStatus.Onboarded], value: RoleApprovalStatus.Onboarded },
				{ key: RoleApprovalStatus[RoleApprovalStatus.Expired], value: RoleApprovalStatus.Expired },
				{ key: RoleApprovalStatus[RoleApprovalStatus.Inactive], value: RoleApprovalStatus.Inactive },
				{ key: RoleApprovalStatus[RoleApprovalStatus.Denied], value: RoleApprovalStatus.Denied }
			];
			const statusFilters: filterOption[] = options.map((option) => ({
				key: option.key,
				value: option.value.toString(),
				selected: false
			}));
			return statusFilters;
		}
	})
});
type VolunteerFilterProps = {
	label: string;
	options: filterOption[];
	setSelected: (selected: string | string[]) => void;
}
function VolunteerFilter({ label, options, setSelected }: VolunteerFilterProps) {
	const handleChange = (event: SelectChangeEvent<string[]>) => {
		const allOptionKeysPlusSelectedOptionValue = event.target.value;
		setSelected(allOptionKeysPlusSelectedOptionValue);
	};

	return (
		<FormControl sx={{ position: 'relative' }}>
			<Select
				labelId={`volunteer${label}Filter`}
				sx={{
					color: options.filter(o => o.selected).length === options.length ? '#bdbdbd' : null,
					'& .MuiSelect-iconOpen': { transform: 'none' }
				}}
				multiple value={options.map(o => o.key)}
				variant="standard" 
				label={`${label} Filters`}
				onChange={handleChange}
				input={<InputBase />}
				IconComponent={FilterListIcon}
				renderValue={() => {
					const selectedOptions = options.filter(o => o.selected);
					return (selectedOptions.length === options.length) ? `${label}: all` : `${label}: ${selectedOptions.length} of ${options.length}`
				}}
			>
				{options.map((option) =>
					<MenuItem key={option.key} value={option.value ?? ''}>
						<Checkbox checked={option.selected} />
						<ListItemText primary={option.key} />
					</MenuItem>
				)}
			</Select>
		</FormControl>
	)
}
//#endregion
function getStatusComparisonValue(s?: string | number | RoleApprovalStatus | null | undefined): number {	
	if (!s) return 0;
	switch (s) {
		case RoleApprovalStatus.Prospective:
		case "Prospective":
		case "prospective":
		case "1":
		case 1:
			return 1;
		case RoleApprovalStatus.Approved:
		case "Approved":
		case "approved":
		case "3":
		case 3:
			return 3;
		case RoleApprovalStatus.Onboarded:
		case "Onboarded":
		case "onboarded":
		case "4":
		case 4:
			return 4;
		case RoleApprovalStatus.Expired:
		case "Expired":
		case "expired":
		case "2":
		case 2:
			return 2;
		case RoleApprovalStatus.Inactive:
		case "Inactive":
		case "inactive":
		case "5":
		case 5:
			return 5;
		case RoleApprovalStatus.Denied:
		case "Denied":
		case "denied":
		case "6":
		case 6:
			return 6;
		default:
			return 0;
	}
}
function checkStatusEquivalence(a?: string | RoleApprovalStatus | null | undefined, b?: string | RoleApprovalStatus | null | undefined): boolean {
	return getStatusComparisonValue(a) == getStatusComparisonValue(b);
}
function familyLastName(family: CombinedFamilyInfo) {
	return family.family!.adults?.filter(adult =>
		family.family!.primaryFamilyContactPersonId === adult.item1?.id)[0]?.item1?.lastName || "";
}

function simplify(input: string) {
  // Strip out common punctuation elements and excessive whitespace, and convert to lowercase
  return input
    .replace(/[.,/#!$%^&*;:{}=\-_`'"'‘’‚‛“”„‟′‵″‶`´~()]/g, "")
    .replace(/\s{2,}/g, " ")
    .toLowerCase();
}

function VolunteerApproval(props: { onOpen: () => void }) {
	const { onOpen } = props;
	useEffect(onOpen);
	const appNavigate = useAppNavigate();
	const [uncheckedFamilies, setUncheckedFamilies] = useState<string[]>([]);

	//#region Role/Status Selection Code		
	const [roleFilters, setRoleFilters] = useRecoilState(roleFiltersState);
	const [statusFilters, setStatusFilters] = useRecoilState(statusFiltersState);
	function getOptionValueFromSelection(allOptionKeysPlusSelectedOptionValue: string | string[]) {
		const optionValue = [...allOptionKeysPlusSelectedOptionValue].filter((s: string) => !isNaN(Number(s)))[0];
		return optionValue ? optionValue : undefined;
	}
	function getUpdatedFilters(filters: filterOption[], optionToUpdate: filterOption) {
		return filters.map(filter => 
			filter.key === optionToUpdate.key
			? { ...filter, selected: !filter.selected }
			: filter);	
	}
	function changeRoleFilterSelection(selection: string | string[]) {
		setUncheckedFamilies([]);
		const filterOptionToUpdate = roleFilters.find(filter => 
			filter.value === getOptionValueFromSelection(selection));
			console.warn(`changing ${filterOptionToUpdate?.key} to ${!filterOptionToUpdate?.selected}`);
		setRoleFilters(getUpdatedFilters(roleFilters, filterOptionToUpdate!));
	}
	function changeStatusFilterSelection(selection: string | string[]) {
		setUncheckedFamilies([]);
		const filterOptionToUpdate = statusFilters.find(filter => 
			filter.value === getOptionValueFromSelection(selection));
			console.warn(`changing ${filterOptionToUpdate?.key} to ${!filterOptionToUpdate?.selected}`);
		setStatusFilters(getUpdatedFilters(statusFilters, filterOptionToUpdate!));
	}
	//#endregion
	
	// The array object returned by Recoil is read-only. We need to copy it before we can do an in-place sort.
	const volunteerFamiliesLoadable = useLoadable(volunteerFamiliesData);
	const volunteerFamilies = (volunteerFamiliesLoadable || []).map(x => x).sort((a, b) =>
		familyLastName(a) < familyLastName(b) ? -1 : familyLastName(a) > familyLastName(b) ? 1 : 0);

	const [filterText, setFilterText] = useState("");

	//#region Family/Individual Filtering Code
	const selectedFamilyRoleKeys = roleFilters.filter(filterOption => (filterOption.selected && filterOption.type !== filterType.Individual)).map(filterOption => filterOption.key);
	const selectedIndividualRoleKeys = roleFilters.filter(filterOption => (filterOption.selected && filterOption.type !== filterType.Family)).map(filterOption => filterOption.key);
	const selectedStatusKeys = statusFilters.filter(filterOption => filterOption.selected).map(filterOption => filterOption.value);
	
	//#region Family-Specific Methods	
	function familyHasNoValidRoles(family: CombinedFamilyInfo) {
		const familyHasNoValidRoles = roleFilters.every(filterOption => {
			return !familyHasSpecificRoleInValidStatus(family, filterOption.key);
		});
		return familyHasNoValidRoles;
	}

	function familyHasNoValidStatuses(family: CombinedFamilyInfo) {
		const familyHasNoValidStatuses = statusFilters.every(filterOption => family.volunteerFamilyInfo?.familyRoleApprovals?.[filterOption.key] === undefined);
		const familyMembers = ((family.volunteerFamilyInfo?.individualVolunteers && Object.entries(family.volunteerFamilyInfo?.individualVolunteers)) || []);
		const familyMembersWithNoValidStatuses = familyMembers.filter(([, volunteer]) => statusFilters.every(filterOption => volunteer.approvalStatusByRole?.[filterOption.key] === undefined));
		return familyHasNoValidStatuses || familyMembersWithNoValidStatuses.length > 0;
	}

	function familyHasSpecificRoleInValidStatus(family: CombinedFamilyInfo, roleName: string) {
		const validStatuses = statusFilters.filter(filterOption => filterOption.key !== `Not Applied`);
		const currentFamilyStatus = family.volunteerFamilyInfo?.familyRoleApprovals?.[roleName]?.currentStatus;
		const result = validStatuses.some(status => checkStatusEquivalence(status.value, currentFamilyStatus));
		return result;
	}

	function familyMeetsFilterCriteria(family: CombinedFamilyInfo) {
		console.group(`Family`);
		if (selectedFamilyRoleKeys.length === 0) {
			if (selectedStatusKeys.length === 0) {
				console.log(selectedIndividualRoleKeys.length === 0);
				console.groupEnd();
				return (selectedIndividualRoleKeys.length === 0);
			}
			const familyHasARoleInASelectedStatus = selectedStatusKeys.some(status => {
				if (status === "Not Applied") {
					const noValidStatuses = familyHasNoValidStatuses(family);
					return noValidStatuses;
				}
				else 
				{
					const familyHasRoleInASelectedStatus = roleFilters.some(roleFilter => {
						console.group(roleFilter.key);						
						console.log(status?.toString());
						console.log(family.volunteerFamilyInfo?.familyRoleApprovals?.[roleFilter.key]?.currentStatus?.toString());
						console.groupEnd();
						return status?.toString() === family.volunteerFamilyInfo?.familyRoleApprovals?.[roleFilter.key]?.currentStatus?.toString(); 						
					});
					return familyHasRoleInASelectedStatus;
				}
			});
			console.log(familyHasARoleInASelectedStatus);
			console.groupEnd();
			return familyHasARoleInASelectedStatus; 
		} 
		const result = selectedFamilyRoleKeys.some(roleName => {
			console.group(roleName);
			const noValidRoles = familyHasNoValidRoles(family);
			const familyHasRole = (roleName !== "Not Applied") 
				? (family.volunteerFamilyInfo?.familyRoleApprovals?.[roleName] !== undefined) 
				: noValidRoles;
			if (!familyHasRole || (roleName === "Not Applied" && noValidRoles)) {
				console.groupEnd();
				return familyHasRole;
			}
			if (selectedStatusKeys.length === 0) {
				const hasRoleInValidStatus = familyHasSpecificRoleInValidStatus(family, roleName);
				console.groupEnd();
				return hasRoleInValidStatus || (roleName === "Not Applied" && familyHasRole);
			}
			const familyHasRoleInSelectedStatus = selectedStatusKeys.some(status => {
				console.group(status);
				console.log(family.volunteerFamilyInfo?.familyRoleApprovals?.[roleName]?.currentStatus);
				const hasStatus = checkStatusEquivalence(status, family.volunteerFamilyInfo?.familyRoleApprovals?.[roleName]?.currentStatus);
				console.log(hasStatus);
				console.log(Number(status) === family.volunteerFamilyInfo?.familyRoleApprovals?.[roleName]?.currentStatus);
				console.groupEnd();
				return Number(status) === family.volunteerFamilyInfo?.familyRoleApprovals?.[roleName]?.currentStatus;
			});
			console.groupEnd();
			return familyHasRoleInSelectedStatus;
		});
		console.log(result);
		console.groupEnd();
		return result;
	}
	//#endregion

	//#region Family Member-Specific Methods	
	function familyMemberHasNoValidRoles(family: CombinedFamilyInfo) {
		const familyMembers = ((family.volunteerFamilyInfo?.individualVolunteers && Object.entries(family.volunteerFamilyInfo?.individualVolunteers)) || []);
		const familyMembersWithNoValidRoles = familyMembers.filter(([, volunteer]) => roleFilters.every(filterOption => {
			return !familyMemberHasSpecificRoleInValidStatus(volunteer, filterOption.key);
		}));
		return (familyMembersWithNoValidRoles.length > 0);
	}

	function familyMemberHasNoValidStatuses(volunteer: VolunteerInfo) {
		const noValidStatuses = statusFilters.every(filterOption => {
			return checkStatusEquivalence(volunteer.approvalStatusByRole?.[filterOption.key].currentStatus, null);
		});
		return noValidStatuses;
	}

	function familyMemberHasSpecificRoleInValidStatus(volunteer: VolunteerInfo, roleName: string) {
		const validStatuses = statusFilters.filter(filterOption => filterOption.key !== `Not Applied`);
		const currentFamilyMemberStatus = volunteer.approvalStatusByRole?.[roleName]?.currentStatus;
		// console.group(`familyMemberHasSpecificRoleInValidStatus`);
		// console.log(currentFamilyMemberStatus);	
		// console.log(JSON.stringify(validStatuses));	
		const result = validStatuses.some(status => {
			const equivalent = checkStatusEquivalence(status.value, currentFamilyMemberStatus);
			return equivalent;
		});
		//console.log(result);
		//console.groupEnd();
		return result;	
	}

	function familyMemberHasARoleInSelectedStatus(volunteer: VolunteerInfo, status: string) {
		let result = false;
		if (status === "Not Applied") {
			const noValidStatuses = familyMemberHasNoValidStatuses(volunteer);
			result = noValidStatuses;
		}
		else {
			const familyMemberHasARoleInSelectedStatus = roleFilters.some(roleFilter => {
				console.group
				return checkStatusEquivalence(volunteer.approvalStatusByRole?.[roleFilter.key]?.currentStatus, status);
			});
			result = familyMemberHasARoleInSelectedStatus;
		}
		console.log(`Member HasRoleInSelectedStatus: ${result}`);
		return result;
	}
	
	function familyMembersMeetFilterCriteria(family: CombinedFamilyInfo) {
		console.group(`Family Members`);	
		const familyMembers = ((family.volunteerFamilyInfo?.individualVolunteers && Object.entries(family.volunteerFamilyInfo?.individualVolunteers)) || []);
		if (selectedIndividualRoleKeys.length === 0) {
			if (selectedStatusKeys.length === 0) {
				console.log(`B 1`);
				console.groupEnd();
				return selectedFamilyRoleKeys.length === 0;
			}
			const familyMemberHasARoleInASelectedStatus = selectedStatusKeys.some(status => {				
				const familyMembersWithRoleInSelectedStatus = familyMembers.filter(([, volunteer]) => {					
					return familyMemberHasARoleInSelectedStatus(volunteer, status ? status : "Not Applied");
				});
				return familyMembersWithRoleInSelectedStatus.length > 0;
			});
			console.log(`B 2`);
			console.groupEnd();
			return familyMemberHasARoleInASelectedStatus; 
		} 
		const result = selectedIndividualRoleKeys.some(roleName => {
			console.log(roleName);
			return familyMembers.some(([, volunteer]) => {
				const noValidRoles = familyMemberHasNoValidRoles(family);
				const familyMembersHaveRole = (roleName !== "Not Applied") 
					? familyMemberHasSpecificRoleInValidStatus(volunteer, roleName)
					: noValidRoles;
				if (!familyMembersHaveRole || (roleName === "Not Applied" && noValidRoles)) {
					console.log(`B 3`);
					return familyMembersHaveRole;
				}
				if (selectedStatusKeys.length === 0) {
					return familyMemberHasSpecificRoleInValidStatus(volunteer, roleName);
				}
				console.log(`B 4`);
				const matchingStatus = selectedStatusKeys.some(status => {
					return Number(status) === volunteer.approvalStatusByRole?.[roleName]?.currentStatus;
				});				
				return matchingStatus;
			});
		});
		console.log(result);
		console.groupEnd();
		return result;
	}
	//#endregion

	function familyOrFamilyMembersMeetFilterCriteria(family: CombinedFamilyInfo) {
		// TODO: Scenarios that aren't currently working: 
			//"Host Family" Role (or any other valid) + "Not Applied" Status should return 3 matches, not 0
			//"Not Applied" Role + Any Valid Status should return 0 matches, not all
		// TODO: Cleanup console logs once troubleshooting is completed
		// TODO: Integrate checkStatusEquivalence() for all filter methods in this file for consistency instead of situationally leveraging other comparison methods
		// TODO: Simplify arrow function syntax once there's no further need for console logs
		console.group(`${familyLastName(family)}`);		
		console.log(JSON.stringify(selectedFamilyRoleKeys));
		console.log(JSON.stringify(selectedIndividualRoleKeys));
		console.log(JSON.stringify(selectedStatusKeys));
		const familyMeetsRoleCriteria = familyMeetsFilterCriteria(family);
		const familyMembersMeetRoleCriteria = familyMembersMeetFilterCriteria(family);
		const familyRolesSelected = selectedFamilyRoleKeys.length > 0;
		const individualRolesSelected = selectedIndividualRoleKeys.length > 0;
		const statusesSelected = selectedStatusKeys.length > 0;
		let result = true;
		if (familyRolesSelected && individualRolesSelected) {
			result = familyMeetsRoleCriteria || familyMembersMeetRoleCriteria;
		} else if (familyRolesSelected) {
			result = familyMeetsRoleCriteria;
		} else if (individualRolesSelected) {
			result = familyMembersMeetRoleCriteria;
		} else if (statusesSelected) {
			result = familyMeetsRoleCriteria || familyMembersMeetRoleCriteria;
		}
		console.log(result);
		console.groupEnd();
		return result;		
	}
	//#endregion

	// Filter volunteer families by name and by applicable roles.
	const filteredVolunteerFamilies = volunteerFamilies.filter(family => /* Filter by name */(
		filterText.length === 0 ||
		family.family?.adults?.some(adult => simplify(`${adult.item1?.firstName} ${adult.item1?.lastName}`).includes(filterText.toLowerCase())) ||
		family.family?.children?.some(child => simplify(`${child?.firstName} ${child?.lastName}`).includes(filterText.toLowerCase()))) &&
		/* Filter by roles & approval status */ (
		familyOrFamilyMembersMeetFilterCriteria(family)
	));

	const selectedFamilies = filteredVolunteerFamilies.filter(family =>
		!uncheckedFamilies.some(f => f === family.family!.id!));
	
	
	useScrollMemory();

	function openFamily(familyId: string) {
		appNavigate.family(familyId);
	}
	const [createVolunteerFamilyDialogOpen, setCreateVolunteerFamilyDialogOpen] = useState(false);

	const theme = useTheme();
	const isMobile = useMediaQuery(theme.breakpoints.down('md'));
	const location = useLocation();

	const [expandedView, setExpandedView] = useLocalStorage('volunteer-approval-expanded', true);
	const handleExpandCollapse = (
		_event: React.MouseEvent<HTMLElement>,
		newExpandedView: boolean | null,
	) => {
		if (newExpandedView !== null) {
		setExpandedView(newExpandedView);
		}
	};

	const { locationId } = useRecoilValue(selectedLocationContextState);
	const organizationConfiguration = useRecoilValue(organizationConfigurationQuery);
	const smsSourcePhoneNumbers = organizationConfiguration?.locations?.find(loc =>
		loc.id === locationId)?.smsSourcePhoneNumbers;
	const [smsMode, setSmsMode] = useState(false);

	function getSelectedFamiliesContactEmails() {
		return selectedFamilies.map(family => {
		const primaryContactPerson = family.family?.adults?.find(adult =>
			adult.item1?.id === family.family?.primaryFamilyContactPersonId);
		const preferredEmailAddress = primaryContactPerson?.item1?.emailAddresses?.find(email =>
			email.id === primaryContactPerson.item1?.preferredEmailAddressId);
		return preferredEmailAddress;
		}).filter(email => typeof email !== 'undefined') as EmailAddress[];
	}

	function copyEmailAddresses() {
		const emailAddresses = getSelectedFamiliesContactEmails();
		navigator.clipboard.writeText(emailAddresses.map(email => email.address).join("; "));
		setNoticeOpen(true);
	}
	const [noticeOpen, setNoticeOpen] = useState(false);

	const windowSize = useWindowSize();

	const permissions = useAllVolunteerFamiliesPermissions();

	useScreenTitle("Volunteers");

	return (!volunteerFamiliesLoadable
		? <ProgressBackdrop>
		<p>Loading families...</p>
		</ProgressBackdrop>
		: <>
		<Grid container sx={{
			paddingRight: smsMode && !isMobile ? '400px' : null,
			height: smsMode && isMobile ? `${windowSize.height - 500 - 24}px` : null,
			overflow: smsMode && isMobile ? 'scroll' : null
		}}>
			<Grid item xs={12}>
			<Stack direction='row-reverse' sx={{ marginTop: 1 }}>
				<ToggleButtonGroup value={expandedView} exclusive onChange={handleExpandCollapse}
				size={isMobile ? 'medium' : 'small'} aria-label="row expansion">
				<ToggleButton value={true} aria-label="expanded"><UnfoldMoreIcon /></ToggleButton>
				<ToggleButton value={false} aria-label="collapsed"><UnfoldLessIcon /></ToggleButton>
				</ToggleButtonGroup>
				<SearchBar value={filterText} onChange={value => {
				setUncheckedFamilies([]);
				setFilterText(value);
				}} />
				{permissions(Permission.SendBulkSms) &&
				<IconButton color="inherit" aria-label="copy email addresses"
					onClick={() => copyEmailAddresses()} sx={{}}>
					<EmailIcon />
				</IconButton>}
				{permissions(Permission.SendBulkSms) && smsSourcePhoneNumbers && smsSourcePhoneNumbers.length > 0 &&
				<IconButton color={smsMode ? 'secondary' : 'inherit'} aria-label="send bulk sms"
					onClick={() => setSmsMode(!smsMode)} sx={{ marginRight: 2 }}>
					<SmsIcon sx={{ position: 'relative', top: 1 }} />
				</IconButton>}
				<Snackbar
				open={noticeOpen}
				autoHideDuration={5000} anchorOrigin={{ vertical: 'top', horizontal: 'center' }}
				onClose={() => setNoticeOpen(false)}
				message={`Found and copied ${getSelectedFamiliesContactEmails().length} email addresses for ${selectedFamilies.length} selected families to clipboard`} />
				<Box sx={{ display: 'flex', flexDirection: 'row', gap: '.75rem', marginRight: '.75rem', alignItems: 'center'}}>
					<VolunteerFilter label="Roles" options={roleFilters} setSelected={changeRoleFilterSelection} />
					<VolunteerFilter label="Statuses" options={statusFilters} setSelected={changeStatusFilterSelection} />
				</Box>
				<ButtonGroup variant="text" color="inherit" aria-label="text inherit button group" style={{ flexGrow: 1 }}>
					<Button color={location.pathname.endsWith("/volunteers/approval") ? 'secondary' : 'inherit'} component={Link} to={"../approval"}>Approvals</Button>
					<Button color={location.pathname.endsWith("/volunteers/progress") ? 'secondary' : 'inherit'} component={Link} to={"../progress"}>Progress</Button>
				</ButtonGroup>				
			</Stack>
			</Grid>
			<Grid item xs={12}>
			<TableContainer>
				<Table sx={{ minWidth: '700px' }} size="small">
				<TableHead>
					<TableRow sx={{ height: '40px' }}>
					{smsMode && 
						<TableCell sx={{ padding: 0, width: '36px' }}>
							<Checkbox size='small' checked={uncheckedFamilies.length === 0}
							onChange={e => e.target.checked
								? setUncheckedFamilies([])
								: setUncheckedFamilies(filteredVolunteerFamilies.map(f => f.family!.id!))} />
						</TableCell>}
						{expandedView ? 
							<TableCell>Last Name, First Name</TableCell>
						:	<TableCell>Family</TableCell>}
						<TableCell>Roles</TableCell>
					</TableRow>
				</TableHead>
				<TableBody>
					{filteredVolunteerFamilies.map((volunteerFamily) => (
					<React.Fragment key={volunteerFamily.family?.id}>
						<TableRow sx={{ backgroundColor: '#eef', height: '39px' }} onClick={() => openFamily(volunteerFamily.family!.id!)}>
							{smsMode && 
							<TableCell key="-" sx={{ padding: 0, width: '36px' }}>
								<Checkbox size='small' checked={!uncheckedFamilies.some(x => x === volunteerFamily.family!.id!)}
								onChange={e => e.target.checked
									? setUncheckedFamilies(uncheckedFamilies.filter(x => x !== volunteerFamily.family!.id!))
									: setUncheckedFamilies(uncheckedFamilies.concat(volunteerFamily.family!.id!))}
								onClick={e => e.stopPropagation()} />
							</TableCell>}
							<TableCell key="1" colSpan={expandedView ? 1 : 1}>
								<Typography sx={{ fontWeight: 600 }}>{familyLastName(volunteerFamily) + " Family"}</Typography>
							</TableCell>
							<TableCell>
								{roleFilters.map((roleFilter, index) =>
									<VolunteerRoleApprovalStatusChip 
										key={index} 
										sx={{ margin: '.125rem .25rem .125rem 0' }}
										roleName={roleFilter.key} 
										status={volunteerFamily.volunteerFamilyInfo?.familyRoleApprovals?.[roleFilter.key]?.effectiveRoleApprovalStatus} 
									/>
								)}
							</TableCell>
						</TableRow>
						{expandedView && volunteerFamily.family?.adults?.map(adult => adult.item1 && adult.item1.active && (
						<TableRow key={volunteerFamily.family?.id + ":" + adult.item1.id}
							onClick={() => openFamily(volunteerFamily.family!.id!)}>
							{smsMode && <TableCell />}
							<TableCell>{adult.item1.lastName}, {adult.item1.firstName}</TableCell> 
							<TableCell>
								{Object.entries(volunteerFamily.volunteerFamilyInfo?.individualVolunteers?.[adult.item1!.id!].approvalStatusByRole || {}).map(([role, roleApprovalStatus]) =>
								<VolunteerRoleApprovalStatusChip key={role} roleName={role} status={roleApprovalStatus.effectiveRoleApprovalStatus} sx={{ margin: '.125rem .25rem .125rem 0' }} />)}                      
							</TableCell>    						
						</TableRow>))}
						{expandedView && volunteerFamily.family?.children?.map(child => child && child.active && (
						<TableRow key={volunteerFamily.family?.id + ":" + child.id}
							onClick={() => openFamily(volunteerFamily.family!.id!)}
							sx={{ color: 'ddd', fontStyle: 'italic' }}>
							{smsMode && <TableCell />}
							<TableCell>{child.lastName}, {child.firstName}</TableCell> 
							<TableCell></TableCell>    						
						</TableRow>))}						
					</React.Fragment>
					))}
				</TableBody>
				</Table>
			</TableContainer>
			{permissions(Permission.EditFamilyInfo) && permissions(Permission.ActivateVolunteerFamily) && <Fab color="primary" aria-label="add"
				sx={{ position: 'fixed', right: '30px', bottom: '70px' }}
				onClick={() => setCreateVolunteerFamilyDialogOpen(true)}>
				<AddIcon />
			</Fab>}
			{createVolunteerFamilyDialogOpen && <CreateVolunteerFamilyDialog onClose={(volunteerFamilyId) => {
				setCreateVolunteerFamilyDialogOpen(false);
				volunteerFamilyId && openFamily(volunteerFamilyId);
			}} />}
			</Grid>
		</Grid>
		{smsMode && <BulkSmsSideSheet selectedFamilies={selectedFamilies}
			onClose={() => setSmsMode(false)} />}
		</>
	);
}

export { VolunteerApproval };
